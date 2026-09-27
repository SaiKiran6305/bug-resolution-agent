# Bug Resolution Agent

A planned developer assistant that investigates bug reports, proposes code changes and regression tests, and presents execution evidence for human review.

## Status

Project design and implementation roadmap. The application is not implemented yet.

## Initial scope

One shared assistant, initially connected to one ASP.NET Core service. Additional services can be registered later.

### Inputs
- Bug title, description, expected and actual behavior, and reproduction steps.
- Optional redacted logs, stack trace, environment, timestamp, and trace ID.
- Repository and exact commit under investigation.
- Later: import a bug ID or issue URL through a configured tracker connection.

### Outputs
- Likely cause and evidence with repository paths, line references, and commit.
- Proposed patch and regression test.
- Actual build and test results, including failure before and success after the fix where reproducible.
- Uncertainty and incomplete verification clearly shown.
- Later: developer-triggered draft pull request.

## Planned stack

- React review interface.
- ASP.NET Core API and background worker.
- Model API behind a provider abstraction.
- PostgreSQL for investigations, evidence, execution records, and feedback.
- Repository text and symbol search for initial retrieval.
- Optional pgvector for semantic search over historical fixes and runbooks.
- Isolated execution runner with disposable test data.

## Workflow

1. Validate the report and select a registered repository at a pinned commit.
2. Extract stack trace paths, symbols, exception types, and other clues.
3. Retrieve relevant code, callers, and tests.
4. Generate a structured diagnosis and proposed reproduction test.
5. Run the reproduction against the original code and record its failure reason.
6. Generate and validate a constrained patch.
7. Run the same regression test after applying the patch and run existing tests.
8. Present the diff, evidence, commands, exit codes, and limitations for developer review.

The model proposes tests; the runner executes them. Passing a build alone does not establish resolution. A mock-based check is recorded separately from verification against a real dependency sandbox.

## Service registration

Each registered service needs a repository, permitted source paths, build and test commands, runtime dependencies, test data setup, and resource limits. Secrets are supplied outside version control. The backend controls permitted commands and actions.

## Architecture decisions

- Use an explicit workflow first; multiple autonomous agents are not required.
- RAG starts with relevant code retrieved by exact search.
- A vector database is optional and added only after retrieval evaluation demonstrates value.
- Investigations run asynchronously with persisted state and bounded retries.
- Repository contents and issue text are untrusted evidence.
- Test runners receive no production credentials or GitHub write tokens.
- Draft PR creation is separate from model-directed execution.
- Cross-service tracing, issue imports, and browser flow tests are later extensions.

## Milestones

- [ ] Intake and service registration.
- [ ] Code retrieval and diagnosis with evidence.
- [ ] Patch and regression-test generation.
- [ ] Isolated reproduction and verification.
- [ ] Review UI, history, and feedback.
- [ ] GitHub issue import and draft PR integration.
- [ ] Multiple services and trace-based investigation.
- [ ] Evaluate hybrid retrieval over resolved issues.

## Evaluation

Create 15–30 synthetic or public bugs with known expected behavior. Hold out a subset when tuning. Measure correct-file retrieval, clean patch application, reproduction success, failure-to-pass transitions, regressions, review acceptance, latency, and model cost. Keep hidden reference fixes out of retrieval.

## Planned layout

- frontend/ — React investigation and review interface.
- src/BugResolution.Api/ — intake, service configuration, and review endpoints.
- src/BugResolution.Worker/ — retrieval and model workflow.
- runner/ — isolated build and test execution.
- samples/ — target service and synthetic bug reports.
- tests/ — workflow and integration tests.
- docs/ — architecture and service onboarding.

## Initial demonstration

A synthetic checkout API bug: successful payment leaves the order in an incorrect state. Reproduce it with an API integration test and test database; propose a patch; show failed-before, passed-after, and existing-suite results. Use simulated payment data and explicitly label the mock boundary.
