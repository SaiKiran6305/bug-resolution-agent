using BugResolution.Api;
using Microsoft.Extensions.Configuration;

var passed = 0; var failed = 0;
void Test(string name, Action action) { try { action(); Console.WriteLine("PASS: " + name); passed++; } catch (Exception ex) { Console.Error.WriteLine($"FAIL: {name}: {ex.Message}"); failed++; } }
void Assert(bool value, string message = "Assertion failed") { if (!value) throw new InvalidOperationException(message); }
void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Unsafe input was accepted."); }
var service = new ServiceDefinition { Id = "test", EditablePrefixes = ["src/"] };
var files = new Dictionary<string, string> { ["src/Checkout.cs"] = "public string Status = \"Pending\";\n", ["tests/Existing.cs"] = "// existing\n" };
var proposal = new Proposal("fix", "wrong status", "test limitation", [new("src/Checkout.cs", 1, 1, "status")], [new("src/Checkout.cs", "\"Pending\"", "\"Paid\"")], "// regression\n");
Test("Reject traversal and absolute paths", () => { foreach (var path in new[] { "../secret", "/etc/passwd", "src/../../x", "src\\x", "C:/x", "src//x", "./x", "a\0b" }) Reject(() => Safety.RelativePath(path)); });
Test("Redact common credentials and emails", () => { var redacted = Safety.Redact("Authorization: Bearer secretvalue password=abc user@example.com"); Assert(!redacted.Contains("secretvalue") && !redacted.Contains("abc") && !redacted.Contains("user@example.com")); });
Test("Apply unique edit without mutating source", () => { var output = Safety.Apply(proposal, files, service); Assert(output["src/Checkout.cs"].Contains("Paid") && files["src/Checkout.cs"].Contains("Pending")); Assert(output.ContainsKey(service.RegressionPath)); });
Test("Reject edits to existing tests", () => Reject(() => Safety.Apply(proposal with { Edits = [new("tests/Existing.cs", "existing", "weakened")] }, files, service)));
Test("Reject unknown files", () => Reject(() => Safety.Apply(proposal with { Edits = [new("src/Missing.cs", "old", "new")] }, files, service)));
Test("Reject ambiguous replacement", () => Reject(() => Safety.Apply(proposal with { Edits = [new("src/Checkout.cs", "\"", "'")] }, files, service)));
Test("Reject invalid citations", () => Reject(() => Safety.Apply(proposal with { Citations = [new("src/Checkout.cs", 0, 99, "invalid")] }, files, service)));
Test("Reject build-file changes", () => Reject(() => Safety.Apply(proposal with { Edits = [new("src/Project.csproj", "old", "new")] }, files, service)));
Test("Rank exact file clues first", () => { var evidence = Repositories.Retrieve(new("test", "Checkout Pending", "Checkout.cs error", "", "", "", ""), files); Assert(evidence[0].Path == "src/Checkout.cs"); });
Test("Compilation failure is not reproduction", () => Assert(!Verification.Reproduced(new("test", "dotnet", 1, false, "compile error", 1, false, 0, 0))));
Test("Timeout is not reproduction", () => Assert(!Verification.Reproduced(new("test", "dotnet", 1, true, "", 1, true, 0, 1))));
Test("Assertion failure is reproduction", () => Assert(Verification.Reproduced(new("test", "dotnet", 1, false, "assertion", 1, true, 0, 1))));
Test("Zero executed tests cannot pass", () => Assert(!Verification.Passed(new("test", "dotnet", 0, false, "", 1, true, 0, 0))));
Test("Review and recovery are durable", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "agent-store-" + Guid.NewGuid().ToString("N"));
    try
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["DataDirectory"] = directory }).Build();
        var store = new Store(config); var run = new Investigation { Report = new("test", "title", "description", "", "", "", ""), Status = "Running" };
        store.Save(run); new Store(config).Recover(); Assert(store.Get(run.Id)?.Status == "Failed");
        run.Status = "AwaitingReview"; store.Save(run); store.Review(run.Id, new("Approved", "Reviewed"));
        Assert(new Store(config).Get(run.Id)?.Review == "Approved");
        try { store.Review(run.Id, new("Rejected", "second")); throw new Exception("Duplicate review accepted"); } catch (InvalidOperationException) { }
    }
    finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
});
if (args.Length == 2 && args[0] == "--export-demo-test") File.WriteAllText(args[1], ModelProvider.DemoProposal().RegressionTest);
Console.WriteLine($"{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;
