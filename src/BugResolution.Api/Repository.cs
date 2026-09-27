using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BugResolution.Api;

public sealed record CommandResult(int ExitCode, string Output, bool TimedOut, long DurationMs);
public static class Commands
{
    public static async Task<CommandResult> Run(string executable, IEnumerable<string> arguments, string? directory, int seconds, CancellationToken ct)
    {
        var info = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = directory ?? Environment.CurrentDirectory };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        // Never pass model-provider or GitHub credentials into child processes.
        foreach (var key in info.Environment.Keys.Where(k => k.Contains("KEY", StringComparison.OrdinalIgnoreCase) || k.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) || k.Contains("SECRET", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
        using var process = new Process { StartInfo = info };
        var timer = Stopwatch.StartNew(); process.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        var output = new StringBuilder(); var gate = new object();
        async Task Drain(StreamReader reader)
        {
            var buffer = new char[4096]; int count;
            while ((count = await reader.ReadAsync(buffer)) != 0)
                lock (gate) { if (output.Length < 1_000_000) output.Append(buffer, 0, Math.Min(count, 1_000_000 - output.Length)); }
        }
        var stdout = Drain(process.StandardOutput); var stderr = Drain(process.StandardError);
        var timedOut = false;
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { timedOut = true; try { process.Kill(true); } catch (InvalidOperationException) { } await process.WaitForExitAsync(CancellationToken.None); }
        await Task.WhenAll(stdout, stderr);
        return new(process.ExitCode, output.ToString(), timedOut, timer.ElapsedMilliseconds);
    }
}
public sealed record Snapshot(Dictionary<string, string> Files, string Revision, string Digest);
public sealed class Repositories(IConfiguration config)
{
    public ServiceDefinition[] Services { get; } = config.GetSection("Services").Get<ServiceDefinition[]>() ?? [];
    public ServiceDefinition Get(string id) => Services.SingleOrDefault(s => s.Id == id) ?? throw new ArgumentException("Unknown service.");
    private static readonly HashSet<string> Extensions = [".cs", ".csproj", ".json", ".md", ".props", ".targets", ".sln", ".slnx", ".config"];
    private static bool Allowed(string path) => Extensions.Contains(Path.GetExtension(path)) && !path.Split('/').Any(p => p is ".git" or "bin" or "obj" or "node_modules" || p.StartsWith('.')) && !path.Contains("secret", StringComparison.OrdinalIgnoreCase) && !path.Contains("appsettings.", StringComparison.OrdinalIgnoreCase);
    public async Task<Snapshot> Read(ServiceDefinition service, CancellationToken ct)
    {
        var root = Path.GetFullPath(service.RepositoryPath); var source = service.SourcePath.Trim('/');
        if (source.Length > 0) Safety.RelativePath(source);
        var files = new Dictionary<string, string>(StringComparer.Ordinal); var revision = "bundled";
        if (service.Bundled)
        {
            var folder = Path.Combine(root, source);
            void Visit(string directory)
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) continue;
                    var relative = Path.GetRelativePath(folder, entry).Replace('\\', '/');
                    if (Directory.Exists(entry)) { if (!relative.Split('/').Any(p => p is "bin" or "obj" or "node_modules" || p.StartsWith('.'))) Visit(entry); }
                    else if (Allowed(relative)) { if (new FileInfo(entry).Length > 100_000) throw new InvalidOperationException("Source file exceeds 100 KB limit."); files[relative] = File.ReadAllText(entry); }
                    if (files.Count > 200) throw new InvalidOperationException("Service snapshot exceeds 200 files.");
                }
            }
            Visit(folder);
        }
        else
        {
            var head = await Commands.Run("git", ["-C", root, "rev-parse", "HEAD"], null, 10, ct);
            if (head.ExitCode != 0 || !Regex.IsMatch(head.Output.Trim(), "^[a-f0-9]{40,64}$")) throw new InvalidOperationException("Registered service must point to a local Git checkout.");
            revision = head.Output.Trim();
            var listing = await Commands.Run("git", ["-C", root, "ls-tree", "-r", "-z", revision, "--", source.Length == 0 ? "." : source], null, 10, ct);
            if (listing.ExitCode != 0 || listing.TimedOut) throw new InvalidOperationException("Could not list repository snapshot.");
            foreach (var record in listing.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = record.Split('\t', 2); if (parts.Length != 2 || !parts[0].StartsWith("100644 blob ") && !parts[0].StartsWith("100755 blob ")) continue;
                var path = parts[1]; var relative = source.Length == 0 ? path : path[(source.Length + 1)..];
                if (!Allowed(relative)) continue;
                Safety.RelativePath(relative);
                if (files.Count >= 200) throw new InvalidOperationException("Service snapshot exceeds 200 files.");
                var content = await Commands.Run("git", ["-C", root, "show", $"{revision}:{path}"], null, 10, ct);
                if (content.ExitCode != 0 || content.TimedOut || content.Output.Length > 100_000) throw new InvalidOperationException("Cannot read a bounded source file.");
                files[relative] = content.Output;
            }
        }
        if (files.Count == 0 || files.Sum(x => x.Value.Length) > 2_000_000) throw new InvalidOperationException("Source snapshot is empty or exceeds 2 MB.");
        var canonical = string.Join("\n", files.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "\0" + x.Value));
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new(files, service.Bundled ? "bundle:" + digest[..12] : revision, digest);
    }
    public static List<Evidence> Retrieve(BugReport report, IReadOnlyDictionary<string, string> files)
    {
        var query = $"{report.Title} {report.Description} {report.Expected} {report.Actual} {report.Logs}";
        var words = Regex.Matches(query, @"[A-Za-z_][A-Za-z0-9_]{3,}").Select(x => x.Value.ToLowerInvariant()).Distinct().Take(80).ToArray();
        return files.Where(f => f.Key.EndsWith(".cs") || f.Key.EndsWith(".md"))
            .Select(f => new Evidence(f.Key, Safety.Redact(f.Value), words.Sum(w => (f.Key.Contains(w, StringComparison.OrdinalIgnoreCase) ? 8 : 0) + (f.Value.Contains(w, StringComparison.OrdinalIgnoreCase) ? 1 : 0))))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Path, StringComparer.Ordinal).Take(12).ToList();
    }
    public static void Write(string directory, IReadOnlyDictionary<string, string> files)
    {
        Directory.CreateDirectory(directory);
        foreach (var (path, contents) in files) { Safety.RelativePath(path); var target = Path.Combine(directory, path); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllText(target, contents); }
    }
    public static string Diff(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
    {
        var diff = new StringBuilder();
        foreach (var (path, content) in after.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            before.TryGetValue(path, out var original); if (original == content) continue;
            static string[] Lines(string? text) => string.IsNullOrEmpty(text) ? [] : (text.EndsWith('\n') ? text[..^1] : text).Split('\n');
            var oldLines = Lines(original); var newLines = Lines(content);
            diff.AppendLine($"diff --git a/{path} b/{path}");
            diff.AppendLine(original is null ? "--- /dev/null" : $"--- a/{path}"); diff.AppendLine($"+++ b/{path}");
            diff.AppendLine($"@@ -{(oldLines.Length == 0 ? 0 : 1)},{oldLines.Length} +{(newLines.Length == 0 ? 0 : 1)},{newLines.Length} @@");
            foreach (var line in oldLines) diff.AppendLine("-" + line);
            if (original is not null && !original.EndsWith('\n')) diff.AppendLine("\\ No newline at end of file");
            foreach (var line in newLines) diff.AppendLine("+" + line);
            if (!content.EndsWith('\n')) diff.AppendLine("\\ No newline at end of file");
        }
        return diff.ToString();
    }
}
