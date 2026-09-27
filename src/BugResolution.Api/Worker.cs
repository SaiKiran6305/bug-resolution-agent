namespace BugResolution.Api;

public sealed class Worker(Store store, Repositories repositories, ModelProvider model, Verification verification, IConfiguration config, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        store.Recover();
        while (!stoppingToken.IsCancellationRequested)
        {
            var run = store.Next();
            if (run is null) { await Task.Delay(750, stoppingToken); continue; }
            var folder = Path.Combine(Path.GetFullPath(config["WorkspaceDirectory"] ?? "workspaces"), run.Id);
            try
            {
                run.Status = "Running"; Event(run, "Snapshot", "Reading a bounded, immutable service snapshot.");
                var service = repositories.Get(run.Report.ServiceId); var snapshot = await repositories.Read(service, stoppingToken);
                run.Revision = snapshot.Revision; run.SourceDigest = snapshot.Digest;
                run.Evidence = Repositories.Retrieve(run.Report, snapshot.Files);
                Event(run, "Retrieve", $"Selected {run.Evidence.Count} files using exact text and symbol clues.");
                var (proposal, usage) = await model.Propose(run, service, stoppingToken);
                // Validate references against only the retrieved files, not unseen source.
                if (proposal.Citations is null || proposal.Citations.Any(c => !run.Evidence.Any(e => e.Path == c.Path))) throw new ArgumentException("Proposal cites evidence that was not retrieved.");
                var patched = Safety.Apply(proposal, snapshot.Files, service);
                run.Proposal = proposal; run.Usage = usage; run.Diff = Repositories.Diff(snapshot.Files, patched);
                Event(run, "Propose", run.Report.Demo ? "Loaded the labeled deterministic demo proposal." : "Validated model edits and evidence locations.");
                if (verification.Enabled)
                {
                    var baseline = Path.Combine(folder, "baseline"); var before = Path.Combine(folder, "before"); var after = Path.Combine(folder, "after");
                    Repositories.Write(baseline, snapshot.Files);
                    var withTest = snapshot.Files.ToDictionary(x => x.Key, x => x.Value); withTest[service.RegressionPath] = proposal.RegressionTest;
                    Repositories.Write(before, withTest); Repositories.Write(after, patched);
                    Event(run, "Verify", "Running the existing baseline suite in an isolated container.");
                    var baselineResult = await verification.Execute("Existing tests before fix", baseline, service, "suite", stoppingToken);
                    run.Executions.Add(baselineResult); store.Save(run);
                    if (!Verification.Passed(baselineResult)) run.Verification = "BaselineFailed";
                    else
                    {
                        var fail = await verification.Execute("Regression before fix", before, service, "regression", stoppingToken);
                        run.Executions.Add(fail); store.Save(run);
                        if (!Verification.Reproduced(fail)) run.Verification = "NotReproduced";
                        else
                        {
                            var pass = await verification.Execute("Regression after fix", after, service, "regression", stoppingToken);
                            run.Executions.Add(pass); store.Save(run);
                            var suite = await verification.Execute("Existing tests after fix", after, service, "suite", stoppingToken);
                            run.Executions.Add(suite);
                            run.Verification = Verification.Passed(pass) && Verification.Passed(suite) ? "Passed" : "Failed";
                        }
                    }
                }
                else run.Verification = "NotConfigured";
                run.Status = "AwaitingReview";
                Event(run, "Review", run.Verification == "Passed" ? "Checks passed. Review the assertions, diff and mock boundaries before approval." : $"Verification: {run.Verification}. No verified-fix claim is made.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { run.Status = "Failed"; run.Error = "Server stopped during investigation. Start a new run to retry."; store.Save(run); }
            catch (Exception ex)
            {
                logger.LogWarning("Investigation {RunId} failed: {Type}", run.Id, ex.GetType().Name);
                run.Status = "Failed";
                run.Error = ex is ArgumentException or InvalidOperationException ? Safety.Redact(ex.Message) : "Investigation failed due to an infrastructure error. Check server logs and configuration.";
                Event(run, "Error", run.Error);
            }
            finally { if (Directory.Exists(folder)) { try { Directory.Delete(folder, true); } catch (IOException) { logger.LogWarning("Could not remove workspace for {RunId}", run.Id); } } }
        }
    }
    private void Event(Investigation run, string stage, string message) { run.Events.Add(new(DateTimeOffset.UtcNow, stage, message)); store.Save(run); }
}
