namespace BugResolution.Api;

public sealed record BugReport(string ServiceId, string Title, string Description, string Expected, string Actual, string Steps, string Logs, bool Demo = false);
public sealed record Citation(string Path, int StartLine, int EndLine, string Reason);
public sealed record Edit(string Path, string OldText, string NewText);
public sealed record Proposal(string Summary, string RootCause, string Uncertainty, Citation[] Citations, Edit[] Edits, string RegressionTest);
public sealed record Evidence(string Path, string Content, int Score);
public sealed record RunEvent(DateTimeOffset At, string Stage, string Message);
public sealed record Execution(string Stage, string Command, int ExitCode, bool TimedOut, string Output, long DurationMs, bool ProtocolValid, int Passed, int Failed);
public sealed record ReviewRequest(string Decision, string Notes);
public sealed record ImportRequest(string ServiceId, string Issue);
public sealed record Usage(int InputTokens, int OutputTokens);
public sealed class Investigation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Queued";
    public required BugReport Report { get; set; }
    public string? Revision { get; set; }
    public string? SourceDigest { get; set; }
    public string? Error { get; set; }
    public string Verification { get; set; } = "NotRun";
    public string? Review { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? PullRequestUrl { get; set; }
    public Proposal? Proposal { get; set; }
    public Usage? Usage { get; set; }
    public List<Evidence> Evidence { get; set; } = [];
    public List<RunEvent> Events { get; set; } = [];
    public List<Execution> Executions { get; set; } = [];
    public string Diff { get; set; } = "";
}
public sealed class ServiceDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string RepositoryPath { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public bool Bundled { get; set; }
    public string[] EditablePrefixes { get; set; } = ["src/"];
    public string RegressionPath { get; set; } = "tests/Checkout.Tests/AgentRegression.cs";
    public string TestProject { get; set; } = "tests/Checkout.Tests/Checkout.Tests.csproj";
    public string RunnerImage { get; set; } = "bug-agent-runner:local";
    public string? GitHubRepository { get; set; }
    public string BaseBranch { get; set; } = "main";
}
public static class JsonDefaults
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true };
}
