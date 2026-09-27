using System.Text.RegularExpressions;

namespace BugResolution.Api;

public static class Safety
{
    public static string RelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || path.Contains(':') || path.Any(char.IsControl)
            || path.Split('/').Any(x => x is ".." or "." or "")) throw new ArgumentException("Unsafe relative path.");
        return path;
    }
    public static string Redact(string input)
    {
        var result = Regex.Replace(input, @"(?i)(authorization\s*[:=]\s*(?:bearer\s+|basic\s+)?|(?:api[_-]?key|password|token|secret)\s*[:=]\s*)[^\s,;]+", "$1[REDACTED]");
        result = Regex.Replace(result, @"\b(?:sk-[A-Za-z0-9_-]{12,}|gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]+)\b", "[REDACTED]");
        return Regex.Replace(result, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", "[EMAIL]");
    }
    public static BugReport Validate(BugReport report)
    {
        if (string.IsNullOrWhiteSpace(report.ServiceId) || string.IsNullOrWhiteSpace(report.Title) || string.IsNullOrWhiteSpace(report.Description))
            throw new ArgumentException("Service, title and description are required.");
        if (report.Title.Length > 200 || new[] { report.Description, report.Expected, report.Actual, report.Steps, report.Logs }.Any(s => s is null || s.Length > 16000))
            throw new ArgumentException("Title must be at most 200 characters; report fields at most 16,000 each.");
        return report with { Title = Redact(report.Title), Description = Redact(report.Description), Expected = Redact(report.Expected), Actual = Redact(report.Actual), Steps = Redact(report.Steps), Logs = Redact(report.Logs) };
    }
    public static Dictionary<string, string> Apply(Proposal proposal, IReadOnlyDictionary<string, string> source, ServiceDefinition service)
    {
        if (proposal.Edits is null || proposal.Citations is null || proposal.Edits.Length is < 1 or > 8 || string.IsNullOrWhiteSpace(proposal.RegressionTest) || proposal.RegressionTest.Length > 24000)
            throw new ArgumentException("Proposal must include 1–8 edits and a bounded regression test.");
        if (string.IsNullOrWhiteSpace(proposal.Summary) || string.IsNullOrWhiteSpace(proposal.RootCause) || proposal.Citations.Length == 0)
            throw new ArgumentException("Proposal must include a summary, root cause and evidence citations.");
        foreach (var citation in proposal.Citations)
        {
            if (!source.TryGetValue(citation.Path, out var content) || citation.StartLine < 1 || citation.EndLine < citation.StartLine || citation.EndLine > content.Split('\n').Length)
                throw new ArgumentException("Proposal cited an invalid source location.");
        }
        var updated = source.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        foreach (var edit in proposal.Edits)
        {
            RelativePath(edit.Path);
            if (!edit.Path.EndsWith(".cs", StringComparison.Ordinal) || !service.EditablePrefixes.Any(p => edit.Path.StartsWith(p, StringComparison.Ordinal)) || !updated.TryGetValue(edit.Path, out var original))
                throw new ArgumentException("The model attempted to edit a protected or unknown file.");
            if (string.IsNullOrEmpty(edit.OldText) || edit.NewText is null || edit.NewText.Length > 24000)
                throw new ArgumentException("Invalid replacement text.");
            var index = original.IndexOf(edit.OldText, StringComparison.Ordinal);
            if (index < 0 || original.IndexOf(edit.OldText, index + edit.OldText.Length, StringComparison.Ordinal) >= 0)
                throw new ArgumentException("Replacement must match exactly one source location.");
            updated[edit.Path] = original[..index] + edit.NewText + original[(index + edit.OldText.Length)..];
        }
        RelativePath(service.RegressionPath);
        updated[service.RegressionPath] = proposal.RegressionTest;
        return updated;
    }
}
