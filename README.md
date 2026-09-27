# Bug Resolution Agent

A developer assistant that turns a bug report into a reviewable diagnosis, proposed patch, regression test and execution evidence.

**React + TypeScript · ASP.NET Core · SQLite · OpenAI Responses · Docker verification · GitHub**

## What is implemented

- Report intake with expected/actual behavior, reproduction steps and logs.
- GitHub issue number/URL import for registered repositories.
- One assistant with multiple operator-configured services.
- Immutable source snapshots, exact code retrieval and cited evidence.
- Live model proposals with a strict schema, plus a clearly labeled demo mode.
- Validated source edits, a generated regression test and downloadable unified diff.
- Optional Docker verification: baseline, failure before fix, success after fix and existing-suite checks.
- Persistent investigations, background processing, review decisions and restart recovery.
- Explicit draft PR creation for approved, verified, unchanged GitHub sources.
- API access key, bounded requests/output, redaction and an isolated runner contract.
- Responsive UI with history, evidence, patch and test-result views.

This is a working single-user starter application, not a claim of production readiness for arbitrary company repositories. Real services require the documented test adapter. Live model and GitHub operations require your credentials. See [architecture and limits](docs/architecture.md).

## Quick start: full local workflow

Requirements: .NET 10 SDK, Node.js 22.12+ (or a supported newer LTS), Python 3, Git, and Docker for isolated verification. Linux and macOS are the documented host paths. Docker Desktop must be running on macOS. On Windows, use WSL2 with Docker integration.

```bash
git clone https://github.com/SaiKiran6305/bug-resolution-agent.git
cd bug-resolution-agent
python3 scripts/setup.py
python3 scripts/build-web.py
docker build -f runner/Dockerfile -t bug-agent-runner:local .
```

Edit `.env` locally and set `RUNNER_ENABLED=true`. Keep the generated `AGENT_API_KEY`. Model and GitHub credentials are optional for the sample.

```bash
python3 scripts/run.py
```

Open **http://127.0.0.1:5080**. Enter the `AGENT_API_KEY` from your local `.env`, then select **Run sample investigation**. The demo uses a predefined patch, but retrieval, validation, persistence and enabled tests actually execute. The expected run has four execution records with exit codes **0, 1, 0, 0**, followed by developer review.

The deliberately buggy sample returns a Pending order after successful payment. Its regression exercises a real local HTTP API. Payments are fake and the order store is in memory; no real money or production data is involved.

### Without Docker

Leave `RUNNER_ENABLED=false`. You can inspect proposals, evidence and diffs. Verification is explicitly shown as **Runner not configured**; no test success is invented.

### Packaged application

```bash
python3 scripts/setup.py
docker compose up --build
```

Open the same URL and enter the local access key. The packaged application runs as a non-root user with durable SQLite storage. Compose does not mount a Docker socket and therefore leaves verification disabled. Use the host setup above to run the full isolated verification workflow.

### Hosted UI

For a persistent hosted demo, see [Deploy to Render](docs/deploy-render.md). It hosts the UI, API and bundled sample with durable investigation history. Hosted Docker test execution is disabled; run the app locally with Docker to use the four-stage verification workflow.

## Enable live investigations

Set `MODEL_API_KEY` and `MODEL_NAME` in `.env` to an OpenAI API key and a model that supports Responses structured outputs. Restart the server. Report/source context is sent to that provider; use only data you are authorized to share. Redaction covers common patterns, not every possible secret.

The model is selected explicitly so provider availability and cost remain your decision. Missing model credentials never fall back to the demo. The implementation uses one bounded proposal request; there are no unbounded autonomous loops.

For your own service, follow [Register a service](docs/register-service.md). The default sample requires no registration. The UI accepts a pasted report or imports a GitHub issue; it does not accept arbitrary repository paths from users.

For draft PRs, configure the service's GitHub repository and a fine-grained `GITHUB_TOKEN` with the documented permissions. Review and approve a verified live run, then explicitly choose **Create draft PR**. The source and remote base commit must still match. Nothing is merged or deployed automatically.

## Development and tests

```bash
dotnet build src/BugResolution.Api
dotnet run --project tests/BugResolution.Tests
npm ci --prefix frontend
npm test --prefix frontend
npm run build --prefix frontend
python3 scripts/smoke.py
```

Full isolation check after building the runner image:

```bash
VERIFY_DOCKER=true python3 scripts/smoke.py
```

For UI hot reload, run the API on port 5080 and `npm run dev --prefix frontend` in another terminal. Vite proxies `/api` to the API. The access key is kept in browser memory only; refreshing requires entering it again.

GitHub Actions builds both applications, runs the checks, executes the four-stage Docker workflow, builds the packaged image and checks that it starts. CI uses no live model or GitHub write credentials.

## Layout

| Path                         | Purpose                                                                  |
| ---------------------------- | ------------------------------------------------------------------------ |
| `frontend/`                  | React report, evidence, diff and review interface                        |
| `src/BugResolution.Api/`     | API, SQLite store, worker, retrieval, providers and runner orchestration |
| `runner/`                    | Restricted offline .NET verification image                               |
| `samples/checkout/`          | Deliberately buggy API and integration-test adapter                      |
| `tests/BugResolution.Tests/` | Policy, persistence and verification checks                              |
| `scripts/`                   | Local setup, startup, builds and HTTP smoke tests                        |
| `docs/`                      | Architecture, security boundaries and service onboarding                 |

## API

Authenticated routes require `X-Api-Key`.

| Method   | Route                                   | Purpose                                           |
| -------- | --------------------------------------- | ------------------------------------------------- |
| GET      | `/health`                               | Process health                                    |
| GET      | `/api/configuration`                    | Service list and feature availability; no secrets |
| GET/POST | `/api/investigations`                   | List or enqueue a report                          |
| GET      | `/api/investigations/{id}`              | Investigation evidence and state                  |
| POST     | `/api/issues/import`                    | Import a GitHub issue into an editable report     |
| POST     | `/api/investigations/{id}/review`       | Record Approved or Rejected                       |
| GET      | `/api/investigations/{id}/patch`        | Download the proposed diff                        |
| POST     | `/api/investigations/{id}/pull-request` | Create an eligible draft PR                       |

## Next extensions

Historical-issue RAG and pgvector, symbol-aware retrieval, cross-service trace investigation, browser flow tests, Jira import, independent hidden-test evaluation, OIDC/RBAC and a distributed job store are future extensions. The current application does not require a vector database.
