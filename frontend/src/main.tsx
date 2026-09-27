import React, { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  Activity,
  ArrowRight,
  Check,
  ChevronRight,
  Code2,
  FileCode2,
  FlaskConical,
  GitPullRequest,
  Layers,
  LockKeyhole,
  Plus,
  Search,
  ShieldCheck,
  Terminal,
  TriangleAlert,
  Zap,
} from "lucide-react";
import { api, downloadPatch, setAccessKey } from "./api";
import { demoReport, verificationLabel } from "./types";
import type { Configuration, Report, Run, Summary } from "./types";
import "./style.css";

function App() {
  const [config, setConfig] = useState<Configuration | null>(null),
    [history, setHistory] = useState<Summary[]>([]),
    [run, setRun] = useState<Run | null>(null);
  const [form, setForm] = useState<Report>({ ...demoReport, demo: false }),
    [page, setPage] = useState<"new" | "run">("new"),
    [tab, setTab] = useState("Overview");
  const [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [auth, setAuth] = useState(false),
    [key, setKey] = useState(""),
    [issue, setIssue] = useState(""),
    [notes, setNotes] = useState("");
  const selected = useRef<string | null>(null);
  async function initialize() {
    try {
      const configuration = await api<Configuration>("/configuration");
      setConfig(configuration);
      setForm(current => configuration.services.some(s => s.id === current.serviceId)
        ? current : {...current, serviceId: configuration.services[0]?.id ?? ""});
      setHistory(await api<Summary[]>("/investigations"));
      setAuth(false);
      setError("");
    } catch (e) {
      setAuth(true);
      setError((e as Error).message);
    }
  }
  useEffect(() => {
    void initialize();
  }, []);
  useEffect(() => {
    const timer = setInterval(() => {
      if (auth) return;
      void api<Summary[]>("/investigations")
        .then(setHistory)
        .catch((e) => setError(e.message));
      const id = selected.current;
      if (id)
        void api<Run>("/investigations/" + id)
          .then((r) => {
            if (selected.current === id) setRun(r);
          })
          .catch((e) => setError(e.message));
    }, 2000);
    return () => clearInterval(timer);
  }, [auth]);
  async function perform(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function open(id: string) {
    await perform(async () => {
      selected.current = id;
      const next = await api<Run>("/investigations/" + id);
      if (selected.current === id) {
        setRun(next);
        setPage("run");
        setTab("Overview");
        setNotes("");
      }
    });
  }
  async function submit(report: Report) {
    await perform(async () => {
      const created = await api<Run>("/investigations", report);
      selected.current = created.id;
      setRun(created);
      setPage("run");
      setTab("Overview");
      setHistory(await api<Summary[]>("/investigations"));
    });
  }
  function field(name: keyof Report, value: string) {
    setForm((current) => ({ ...current, [name]: value }));
  }
  const review = async (decision: string) =>
    perform(async () => {
      if (run)
        setRun(
          await api<Run>(`/investigations/${run.id}/review`, {
            decision,
            notes,
          }),
        );
    });
  const active = history.filter((r) =>
    ["Queued", "Running"].includes(r.status),
  ).length;
  return (
    <div className="shell">
      <aside className="sidebar">
        <a
          className="brand"
          href="#"
          onClick={(e) => {
            e.preventDefault();
            selected.current = null;
            setPage("new");
          }}
        >
          <span className="brandmark">
            <Code2 size={23} />
          </span>
          <span>
            Resolve<span className="brand-sub">BUG RESOLUTION AGENT</span>
          </span>
        </a>
        <button
          className="new-button"
          onClick={() => {
            selected.current = null;
            setPage("new");
            setError("");
          }}
        >
          <Plus size={17} /> New investigation
        </button>
        <div className="nav-label">WORKSPACE</div>
        <div className="nav-item">
          <Layers size={17} /> Investigations <span>{history.length}</span>
        </div>
        <div className="nav-label recent">RECENT ACTIVITY</div>
        <div className="history">
          {history.length === 0 ? (
            <p className="empty-history">
              Your investigations will appear here.
            </p>
          ) : (
            history.map((item) => (
              <button
                key={item.id}
                className={`history-item ${run?.id === item.id && page === "run" ? "selected" : ""}`}
                onClick={() => void open(item.id)}
              >
                <span className={"dot " + item.status.toLowerCase()} />
                <div>
                  <strong>{item.title}</strong>
                  <small>
                    {item.demo ? "Demo · " : ""}
                    {item.status} ·{" "}
                    {new Date(item.createdAt).toLocaleDateString()}
                  </small>
                </div>
              </button>
            ))
          )}
        </div>
        <div className="sidebar-footer">
          <ShieldCheck size={19} />
          <div>
            Developer review required
            <small>Every change stays reviewable.</small>
          </div>
        </div>
      </aside>
      <main>
        <header className="topbar">
          <span>
            Workspace <ChevronRight size={14} />{" "}
            {page === "new" ? "New investigation" : "Investigation"}
          </span>
          <span className="private">
            <LockKeyhole size={13} /> Private workspace
          </span>
        </header>
        <div className="content">
          {error && (
            <div className="alert error" role="alert">
              <TriangleAlert size={18} />
              {error}
              <button aria-label="Dismiss error" onClick={() => setError("")}>
                ×
              </button>
            </div>
          )}
          {auth ? (
            <section className="panel login">
              <LockKeyhole size={30} />
              <h1>Connect to your workspace</h1>
              <p>Enter the application access key configured on your server.</p>
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  setAccessKey(key);
                  void initialize();
                }}
              >
                <label>
                  Access key
                  <input
                    type="password"
                    autoComplete="off"
                    value={key}
                    onChange={(e) => setKey(e.target.value)}
                    required
                  />
                </label>
                <button className="primary">
                  Connect <ArrowRight size={16} />
                </button>
              </form>
              <small>
                The key stays in memory and clears when you refresh.
              </small>
            </section>
          ) : page === "new" ? (
            <>
              <div className="page-heading">
                <div className="eyebrow">FROM REPORT TO REVIEW</div>
                <h1>Let’s investigate the bug.</h1>
                <p>
                  Bring the report. Get relevant code, a proposed fix, and
                  evidence you can inspect.
                </p>
              </div>
              <div className="stats">
                <div>
                  <span className="stat-icon">
                    <Search size={20} />
                  </span>
                  <div>
                    <strong>{config?.services.length ?? 0}</strong>
                    <small>Connected services</small>
                  </div>
                </div>
                <div>
                  <span className="stat-icon">
                    <Activity size={20} />
                  </span>
                  <div>
                    <strong>{active}</strong>
                    <small>Investigations in progress</small>
                  </div>
                </div>
                <div>
                  <span className="stat-icon">
                    <FlaskConical size={20} />
                  </span>
                  <div>
                    <strong className="stat-text">
                      {config?.runnerEnabled ? "Enabled" : "Not configured"}
                    </strong>
                    <small>Isolated test runner</small>
                  </div>
                </div>
              </div>
              <div className="new-grid">
                <section className="panel">
                  <div className="panel-heading">
                    <span>
                      <FileCode2 size={19} /> Bug report
                    </span>
                    <span className="tag">MANUAL INPUT</span>
                  </div>
                  <form
                    className="report-form"
                    onSubmit={(e) => {
                      e.preventDefault();
                      void submit({ ...form, demo: false });
                    }}
                  >
                    <label>
                      Service
                      <select
                        value={form.serviceId}
                        onChange={(e) => field("serviceId", e.target.value)}
                      >
                        {config?.services.map((s) => (
                          <option key={s.id} value={s.id}>
                            {s.name}
                          </option>
                        ))}
                      </select>
                    </label>
                    <div className="import-row">
                      <input
                        aria-label="GitHub issue number or URL"
                        placeholder="GitHub issue # or URL"
                        value={issue}
                        onChange={(e) => setIssue(e.target.value)}
                      />
                      <button
                        type="button"
                        className="secondary"
                        disabled={
                          busy ||
                          !issue ||
                          !config?.services.find((s) => s.id === form.serviceId)
                            ?.gitHubRepository
                        }
                        onClick={() =>
                          void perform(async () => {
                            setForm(
                              await api<Report>("/issues/import", {
                                serviceId: form.serviceId,
                                issue,
                              }),
                            );
                          })
                        }
                      >
                        Import issue
                      </button>
                    </div>
                    <label>
                      Bug title
                      <input
                        maxLength={200}
                        required
                        value={form.title}
                        onChange={(e) => field("title", e.target.value)}
                      />
                    </label>
                    <label>
                      Description
                      <textarea
                        required
                        maxLength={16000}
                        rows={3}
                        value={form.description}
                        onChange={(e) => field("description", e.target.value)}
                      />
                    </label>
                    <div className="two-col">
                      <label>
                        Expected behavior
                        <textarea
                          maxLength={16000}
                          rows={3}
                          value={form.expected}
                          onChange={(e) => field("expected", e.target.value)}
                        />
                      </label>
                      <label>
                        Actual behavior
                        <textarea
                          maxLength={16000}
                          rows={3}
                          value={form.actual}
                          onChange={(e) => field("actual", e.target.value)}
                        />
                      </label>
                    </div>
                    <label>
                      Steps to reproduce
                      <textarea
                        maxLength={16000}
                        rows={2}
                        value={form.steps}
                        onChange={(e) => field("steps", e.target.value)}
                      />
                    </label>
                    <label>
                      Logs or stack trace{" "}
                      <span className="optional">
                        Optional · remove sensitive data
                      </span>
                      <textarea
                        className="mono"
                        maxLength={16000}
                        rows={3}
                        value={form.logs}
                        onChange={(e) => field("logs", e.target.value)}
                      />
                    </label>
                    <div className="form-footer">
                      <small>
                        {config?.modelConfigured
                          ? "Model connected · code is sent to your configured provider."
                          : "Configure a model key to investigate custom reports."}
                      </small>
                      <button
                        className="primary"
                        disabled={busy || !config?.modelConfigured}
                      >
                        Investigate <ArrowRight size={16} />
                      </button>
                    </div>
                  </form>
                </section>
                <aside className="guide">
                  <section className="demo-card">
                    <span className="demo-icon">
                      <Zap size={22} />
                    </span>
                    <span className="eyebrow">TRY THE WORKFLOW</span>
                    <h2>
                      A checkout bug,
                      <br />
                      ready to investigate.
                    </h2>
                    <p>
                      Follow a sample order that stays Pending after successful
                      payment.
                    </p>
                    <button
                      className="primary"
                      disabled={busy || !config?.services.some(s => s.id === 'checkout-demo' && s.bundled)}
                      onClick={() => void submit({ ...demoReport })}
                    >
                      Run sample investigation <ArrowRight size={16} />
                    </button>
                    <small>
                      Deterministic proposal. No model API key needed. Test
                      results are only shown when executed.
                    </small>
                  </section>
                  <section className="steps">
                    <h3>What happens next</h3>
                    {[
                      [
                        "01",
                        "Locate the evidence",
                        "Find relevant source code and tests.",
                      ],
                      [
                        "02",
                        "Propose a change",
                        "Inspect the explanation and exact diff.",
                      ],
                      [
                        "03",
                        "Verify and review",
                        "See execution results and decide.",
                      ],
                    ].map(([n, title, body]) => (
                      <div key={n}>
                        <span>{n}</span>
                        <div>
                          <strong>{title}</strong>
                          <p>{body}</p>
                        </div>
                      </div>
                    ))}
                  </section>
                </aside>
              </div>
            </>
          ) : run ? (
            <>
              <div className="page-heading">
                <div className="eyebrow">
                  INVESTIGATION / {run.id.slice(0, 8)}{" "}
                  {run.report.demo && <span className="tag">DEMO</span>}
                </div>
                <h1 className="run-title">{run.report.title}</h1>
                <p>
                  {run.report.serviceId} ·{" "}
                  {new Date(run.createdAt).toLocaleString()}
                </p>
              </div>
              <div className="run-summary">
                <span className="status">
                  <span className={"dot " + run.status.toLowerCase()} />
                  {run.status}
                </span>
                <span
                  className={run.verification === "Passed" ? "good" : "muted"}
                >
                  <FlaskConical size={16} />
                  {verificationLabel(run.verification)}
                </span>
                <span className="revision">
                  {run.revision ?? "Waiting for snapshot"}
                </span>
              </div>
              {run.error && <div className="alert error">{run.error}</div>}
              {run.report.demo && (
                <div className="alert info">
                  Demo uses a predefined fix for the sample service. Source
                  retrieval and enabled test execution are real.
                </div>
              )}
              <div className="tabs" role="tablist">
                {["Overview", "Evidence", "Patch", "Tests"].map((name) => (
                  <button
                    role="tab"
                    aria-selected={tab === name}
                    key={name}
                    className={tab === name ? "active" : ""}
                    onClick={() => setTab(name)}
                  >
                    {name}
                    {name === "Tests" && <span>{run.executions.length}</span>}
                  </button>
                ))}
              </div>
              {tab === "Overview" && (
                <div className="detail-grid">
                  <section className="panel prose">
                    <h2>
                      {run.proposal ? "Diagnosis" : "Investigation in progress"}
                    </h2>
                    {run.proposal ? (
                      <>
                        <p>{run.proposal.summary}</p>
                        <h3>Likely cause</h3>
                        <p>{run.proposal.rootCause}</p>
                        <h3>Limits of this result</h3>
                        <p>{run.proposal.uncertainty}</p>
                        {run.usage && (
                          <small>
                            Model usage:{" "}
                            {run.usage.inputTokens.toLocaleString()} input /{" "}
                            {run.usage.outputTokens.toLocaleString()} output
                            tokens
                          </small>
                        )}
                      </>
                    ) : (
                      <p>
                        The worker will retrieve source, prepare a proposal, and
                        run configured checks.
                      </p>
                    )}
                    <h3>Activity</h3>
                    <ol className="timeline">
                      {run.events.map((event, i) => (
                        <li key={i}>
                          <span className="timeline-icon">
                            <Check size={12} />
                          </span>
                          <div>
                            <strong>{event.stage}</strong>
                            <p>{event.message}</p>
                            <small>
                              {new Date(event.at).toLocaleTimeString()}
                            </small>
                          </div>
                        </li>
                      ))}
                    </ol>
                  </section>
                  <section className="panel prose review">
                    <ShieldCheck size={24} />
                    <h2>Developer review</h2>
                    <p>
                      Check the diff, assertions, and execution output before
                      accepting a suggestion.
                    </p>
                    {run.status === "AwaitingReview" ? (
                      <>
                        <label>
                          Review notes
                          <textarea
                            rows={4}
                            maxLength={4000}
                            value={notes}
                            onChange={(e) => setNotes(e.target.value)}
                          />
                        </label>
                        <button
                          className="primary"
                          disabled={busy}
                          onClick={() => void review("Approved")}
                        >
                          Approve proposal
                        </button>
                        <button
                          className="secondary"
                          disabled={busy}
                          onClick={() => void review("Rejected")}
                        >
                          Reject proposal
                        </button>
                      </>
                    ) : (
                      <p className="review-state">
                        {run.review ??
                          "Available when investigation completes."}
                      </p>
                    )}
                    {run.reviewNotes && <p>{run.reviewNotes}</p>}
                    {run.status === "Approved" &&
                      run.verification === "Passed" &&
                      !run.report.demo &&
                      !run.pullRequestUrl && (
                        <button
                          className="primary"
                          disabled={busy || !config?.githubConfigured}
                          onClick={() =>
                            void perform(async () =>
                              setRun(
                                await api<Run>(
                                  `/investigations/${run.id}/pull-request`,
                                  {},
                                ),
                              ),
                            )
                          }
                        >
                          <GitPullRequest size={16} /> Create draft PR
                        </button>
                      )}
                    {run.pullRequestUrl && (
                      <a
                        className="primary"
                        href={run.pullRequestUrl}
                        target="_blank"
                        rel="noreferrer"
                      >
                        Open draft PR
                      </a>
                    )}
                    <small>
                      Approval records your decision. It does not merge or
                      deploy code.
                    </small>
                  </section>
                </div>
              )}
              {tab === "Evidence" && (
                <section className="panel prose">
                  <h2>Retrieved source</h2>
                  <p>
                    Files from revision <code>{run.revision}</code>. Citations
                    reference this snapshot.
                  </p>
                  {run.evidence.map((file) => (
                    <details key={file.path}>
                      <summary>
                        <FileCode2 size={15} />
                        {file.path}
                        <span className="optional">relevance {file.score}</span>
                      </summary>
                      {run.proposal?.citations
                        .filter((c) => c.path === file.path)
                        .map((c, i) => (
                          <p className="citation" key={i}>
                            Lines {c.startLine}–{c.endLine}: {c.reason}
                          </p>
                        ))}
                      <pre>
                        {file.content
                          .split("\n")
                          .map(
                            (line, i) =>
                              `${String(i + 1).padStart(3)}  ${line}`,
                          )
                          .join("\n")}
                      </pre>
                    </details>
                  ))}
                </section>
              )}
              {tab === "Patch" && (
                <section className="panel prose">
                  <div className="section-title">
                    <h2>Proposed changes</h2>
                    <button
                      className="secondary"
                      disabled={!run.diff}
                      onClick={() => void perform(() => downloadPatch(run.id))}
                    >
                      Download patch
                    </button>
                  </div>
                  {run.diff ? (
                    <pre className="diff">
                      {run.diff.split("\n").map((line, i) => (
                        <div
                          key={i}
                          className={
                            line.startsWith("+")
                              ? "addition"
                              : line.startsWith("-")
                                ? "deletion"
                                : line.startsWith("@@")
                                  ? "hunk"
                                  : ""
                          }
                        >
                          {line || " "}
                        </div>
                      ))}
                    </pre>
                  ) : (
                    <p>No patch has been generated.</p>
                  )}
                </section>
              )}
              {tab === "Tests" && (
                <section className="panel prose">
                  <h2>Verification evidence</h2>
                  <p>
                    {run.executions.length === 0
                      ? "No tests have run. Enable the isolated Docker runner to collect verification results."
                      : "Review the commands, assertions, exit codes and output. Passing generated tests does not prove all behavior is correct."}
                  </p>
                  {run.executions.map((execution, i) => (
                    <details key={i} open>
                      <summary>
                        <Terminal size={16} />
                        {execution.stage}
                        <span
                          className={
                            execution.exitCode === 0 ? "good" : "muted"
                          }
                        >
                          exit {execution.exitCode} ·{" "}
                          {(execution.durationMs / 1000).toFixed(1)}s
                        </span>
                      </summary>
                      <p>
                        <code>{execution.command}</code>
                      </p>
                      <small>
                        {execution.timedOut ? "Timed out · " : ""}
                        {execution.protocolValid
                          ? `${execution.passed} passed / ${execution.failed} failed`
                          : "No valid test summary; inspect infrastructure or build errors."}
                      </small>
                      <pre>{execution.output}</pre>
                    </details>
                  ))}
                </section>
              )}
            </>
          ) : null}
          <footer className="footer">
            Built for evidence, review, and reproducible fixes.
          </footer>
        </div>
      </main>
    </div>
  );
}
createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
