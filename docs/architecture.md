# Architecture and operating boundaries

## Request lifecycle

React → ASP.NET Core API → SQLite durable queue → background worker → source snapshot → retrieval → proposal provider → validation → isolated verification → developer review → optional draft PR.

The worker uses an explicit, bounded workflow, with one worker per database. Queued runs survive restarts. Interrupted Running runs become Failed with an explanation; model calls are not silently repeated. Polling updates the UI. Each run records its report, revision, content digest, evidence, proposal, diff, verification commands, exit codes, bounded output, token usage and review.

## Two proposal modes

- **Demo:** deterministic proposal for the bundled checkout fixture, clearly labeled throughout the UI. This path needs no model key. Repository reading, validation, persistence and enabled Docker execution still run normally.
- **Live:** one bounded OpenAI Responses request with a strict JSON schema. Configure a compatible model explicitly. Missing credentials, refusal, truncation or invalid edits produce a failed investigation, never a silent demo fallback. Source and report text are sent to the provider. No model result can directly run a command or create a PR.

There are no embeddings or vector database dependencies. Retrieval ranks file names and source matches. This baseline is intentionally easy to evaluate; add symbol parsing, dependency traversal and historical-issue hybrid retrieval after collecting localization misses.

## Source snapshots

The bundled sample uses a SHA-256 content digest and a `bundle:` revision. Real services read the registered checkout's committed HEAD using Git plumbing; uncommitted working tree edits are excluded. Only regular tracked files with permitted extensions enter the snapshot. Symlinks, dotfiles, build outputs and selected secret/config names are excluded. Limits: 200 files, 100 KB per file, 2 MB total and 100 KB model context. Large services should be registered with narrower source roots or extended retrieval. Known credential patterns and emails are redacted before storage/model context, but redaction is not a guarantee: only supply source and logs you are authorized to share.

Proposed modifications are unique exact replacements in existing `.cs` files under administrator-selected prefixes. The model may replace only the configured regression file in tests. Build files, dependencies and existing tests remain protected. Every citation must refer to retrieved source and valid lines.

## Verification

The API invokes fixed Docker arguments. Model-generated source executes only inside a disposable container. The sandbox receives source as a read-only mount, no host socket or credentials, no external network, a read-only root filesystem, writable bounded tmpfs, a non-root user, reduced Linux capabilities, CPU/memory/PID limits and a timeout. Timed-out containers are explicitly removed.

Stages: existing suite on original source; generated regression on original source; same regression after the patch; existing suite after the patch. Compiler errors, setup errors, missing summaries and timeouts do not count as reproduced bugs. The test adapter returns `BUG_AGENT_RESULT:` JSON. That is test output, not a trusted security attestation: generated code can affect in-container output. Review assertions, and use independent hidden tests for stronger evaluation.

The sample runner uses a real HTTP server and a deterministic fake payment service with an in-memory order store. It does not exercise an external payment processor, persistent database or distributed transaction. The fixture's pre-existing suite deliberately lacks the Paid-state assertion; the generated regression adds it.

## Authentication and deployment

This is a **single-user, single-instance application**. API requests use one server access key; the UI retains it only in memory. Outside Development, startup requires a random key of at least 24 characters. Development without a key is restricted to loopback requests. There is no user identity, multi-tenant authorization, RBAC or SSO. Bind to localhost for local use. Shared deployment requires TLS, an identity layer, per-repository authorization, backups, retention controls and an audited runner host.

The API host needs Docker access for verification. Docker daemon access is powerful; run this service on a dedicated development runner host, not a production host. The packaged Docker Compose app deliberately does not mount the host Docker socket. Use the documented host setup for actual isolated verification. Do not enable host execution of arbitrary model output.

## GitHub

Issue import is scoped to the selected service's configured owner/repository. PR creation is explicit, uses a server-side token, and requires developer approval, passed checks, a non-demo run and unchanged local/remote base SHA. A draft PR contains the patch, regression test, explanation and limitations. It is never merged or deployed. Publication is serialized and existing PRs are returned on retries. If publication stops after creating a branch but before creating the PR, the branch is left for human inspection and never force-updated.

Use a fine-grained GitHub token restricted to the relevant repository: issues read for import; contents read/write and pull requests read/write for draft PRs. The runtime token is separate from GitHub Actions' read-only CI token.

## Known limits and next work

- Explicit test adapter contract; arbitrary repositories do not work without registration and adapter setup.
- Single snapshot and proposal, without iterative model repair loops.
- No Jira import, cross-service tracing, browser tests, vector retrieval or production deployment automation yet.
- Local SQLite storage; migrate to a server database and durable distributed job leases before horizontal scaling.
- Bounded output and cleanup, but no automatic database retention schedule; operators own backups and deletion.
- Model calls and GitHub writes need real credentials for live integration testing.
