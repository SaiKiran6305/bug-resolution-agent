# Register a service

The runtime reads `src/BugResolution.Api/appsettings.Local.json` (ignored by Git). Configuration is operator-controlled; the UI cannot choose arbitrary filesystem paths or commands.

```json
{
  "Services": [
    {
      "Id": "orders",
      "Name": "Orders API",
      "RepositoryPath": "/absolute/path/to/your/checkout",
      "SourcePath": "services/orders",
      "Bundled": false,
      "EditablePrefixes": ["src/Orders.Api/"],
      "RegressionPath": "tests/Orders.Tests/AgentRegression.cs",
      "TestProject": "tests/Orders.Tests/Orders.Tests.csproj",
      "RunnerImage": "orders-agent-runner:local",
      "GitHubRepository": "your-owner/your-repository",
      "BaseBranch": "main"
    }
  ]
}
```

All source, editable, regression and project paths are relative to SourcePath. Editable prefixes should end in `/`. Keep source files and test adapters committed: the agent reads committed HEAD, not working-tree changes. Add further Services entries to retain the sample alongside your service. Environment variables override local JSON configuration when supplied.

## Test adapter contract

Start from `samples/checkout/tests/Checkout.Tests/Program.cs`. The configured console test project starts its application and dispatches on `BUG_AGENT_TEST_MODE`:

- `suite`: run existing independent regression checks; do not invoke AgentRegression.
- `regression`: invoke `AgentRegression.RunAsync(HttpClient client)` against the test application.

The final output line must be `BUG_AGENT_RESULT:{"mode":"regression","passed":0,"failed":1}` (with actual counts). Exit 0 for passed tests, 1 for assertion failures and 2 for infrastructure failures. At least one assertion must execute. For xUnit/NUnit suites, implement an adapter that converts real test-runner results into this protocol while preserving setup failures.

The sandbox is offline. If your service uses NuGet packages, prepare a dedicated, reviewed runner image containing the locked dependency cache, and copy that cache into writable temporary storage in its entrypoint. Do not restore arbitrary model-selected dependencies or enable external network access for generated code. Use local fake dependencies or a deliberately isolated integration environment when extending beyond a single service. The sample image supports SDK-only .NET projects.

Build the image, set Runner\_\_Enabled=true, then validate an existing known-good run before using model proposals. GitHub issue import accepts an issue number, `#123`, or an exact matching GitHub issue URL. Bare Jira IDs are not supported.
