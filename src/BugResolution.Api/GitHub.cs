using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BugResolution.Api;

public sealed class GitHub(IHttpClientFactory factory, IConfiguration config, Repositories repositories, Store store)
{
    private readonly SemaphoreSlim publishLock = new(1);
    public bool Configured => !string.IsNullOrWhiteSpace(config["GitHub:Token"]);
    private static string Repo(ServiceDefinition service)
    {
        if (service.GitHubRepository is null || !Regex.IsMatch(service.GitHubRepository, @"\A[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\z")) throw new ArgumentException("This service has no configured GitHub repository.");
        return service.GitHubRepository;
    }
    private async Task<JsonElement> Send(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, "https://api.github.com/" + path);
        request.Headers.UserAgent.ParseAdd("BugResolutionAgent/1.0"); request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (Configured) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config["GitHub:Token"]);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await factory.CreateClient("github").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"GitHub request failed (HTTP {(int)response.StatusCode}). Check repository access and token permissions.");
        var text = await response.Content.ReadAsStringAsync(ct);
        if (text.Length > 2_000_000) throw new InvalidOperationException("GitHub response exceeds the size limit.");
        return JsonDocument.Parse(text).RootElement.Clone();
    }
    public async Task<BugReport> Import(ImportRequest input, CancellationToken ct)
    {
        var repo = Repo(repositories.Get(input.ServiceId)); var issue = input.Issue?.Trim() ?? "";
        if (Uri.TryCreate(issue, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme != "https" || uri.Host != "github.com" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || !uri.AbsolutePath.StartsWith($"/{repo}/issues/", StringComparison.Ordinal))
                throw new ArgumentException("Use an issue URL from this service's configured GitHub repository.");
            issue = uri.AbsolutePath[($"/{repo}/issues/").Length..];
        }
        if (!int.TryParse(issue.TrimStart('#'), out var number) || number < 1) throw new ArgumentException("Enter a positive issue number or matching GitHub issue URL.");
        var result = await Send(HttpMethod.Get, $"repos/{repo}/issues/{number}", null, ct);
        if (result.TryGetProperty("pull_request", out _)) throw new ArgumentException("This number belongs to a pull request, not a bug report.");
        var body = result.GetProperty("body").GetString() ?? "No issue description provided.";
        return Safety.Validate(new(input.ServiceId, result.GetProperty("title").GetString()!, body, "See imported issue", "See imported issue", "See imported issue", ""));
    }
    public async Task<Investigation> Publish(string id, CancellationToken ct)
    {
        await publishLock.WaitAsync(ct);
        try
        {
            var run = store.Get(id) ?? throw new KeyNotFoundException();
            if (run.PullRequestUrl is not null) return run;
            if (run.Status != "Approved" || run.Verification != "Passed" || run.Proposal is null || run.Report.Demo)
                throw new InvalidOperationException("Draft PRs require an approved, verified, non-demo investigation.");
            if (!Configured) throw new InvalidOperationException("Configure GitHub__Token to create draft pull requests.");
            var service = repositories.Get(run.Report.ServiceId); var repo = Repo(service);
            var snapshot = await repositories.Read(service, ct);
            if (snapshot.Revision != run.Revision || snapshot.Digest != run.SourceDigest) throw new InvalidOperationException("Source changed since investigation. Start a new run before publishing.");
            var remote = await Send(HttpMethod.Get, $"repos/{repo}/git/ref/heads/{Uri.EscapeDataString(service.BaseBranch)}", null, ct);
            if (remote.GetProperty("object").GetProperty("sha").GetString() != run.Revision) throw new InvalidOperationException("GitHub base branch moved. Investigate its current commit before publishing.");
            var branch = "bug-agent/" + run.Id;
            var existing = await Send(HttpMethod.Get, $"repos/{repo}/pulls?state=all&head={Uri.EscapeDataString(repo.Split('/')[0] + ":" + branch)}", null, ct);
            if (existing.GetArrayLength() > 0) { run.PullRequestUrl = existing[0].GetProperty("html_url").GetString(); store.Save(run); return run; }
            var commit = await Send(HttpMethod.Get, $"repos/{repo}/git/commits/{run.Revision}", null, ct);
            var patched = Safety.Apply(run.Proposal, snapshot.Files, service);
            var prefix = service.SourcePath.Trim('/');
            var tree = patched.Where(x => !snapshot.Files.TryGetValue(x.Key, out var old) || old != x.Value).Select(x => new { path = prefix.Length == 0 ? x.Key : prefix + "/" + x.Key, mode = "100644", type = "blob", content = x.Value }).ToArray();
            var createdTree = await Send(HttpMethod.Post, $"repos/{repo}/git/trees", new { base_tree = commit.GetProperty("tree").GetProperty("sha").GetString(), tree }, ct);
            var createdCommit = await Send(HttpMethod.Post, $"repos/{repo}/git/commits", new { message = "fix: " + run.Report.Title, tree = createdTree.GetProperty("sha").GetString(), parents = new[] { run.Revision } }, ct);
            // A failed publish can leave this branch. Reuse only if it points at the same parent.
            var branches = await Send(HttpMethod.Get, $"repos/{repo}/git/matching-refs/heads/{Uri.EscapeDataString(branch)}", null, ct);
            if (branches.GetArrayLength() == 0) await Send(HttpMethod.Post, $"repos/{repo}/git/refs", new { @ref = "refs/heads/" + branch, sha = createdCommit.GetProperty("sha").GetString() }, ct);
            else throw new InvalidOperationException("A previous publish created this branch but no PR. Review the branch on GitHub before retrying; it has not been overwritten.");
            var explanation = $"## Proposed fix\n{run.Proposal.Summary}\n\n## Evidence\n{run.Proposal.RootCause}\n\n## Verification\nRegression failed before and passed after the patch; existing checks passed. Generated tests remain subject to developer review.\n\n## Limitations\n{run.Proposal.Uncertainty}\n\nInvestigation: `{run.Id}`\nBase commit: `{run.Revision}`";
            var pr = await Send(HttpMethod.Post, $"repos/{repo}/pulls", new { title = "fix: " + run.Report.Title, body = explanation, head = branch, @base = service.BaseBranch, draft = true }, ct);
            run.PullRequestUrl = pr.GetProperty("html_url").GetString(); store.Save(run); return run;
        }
        finally { publishLock.Release(); }
    }
}
