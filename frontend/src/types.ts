export type Report = {
  serviceId: string;
  title: string;
  description: string;
  expected: string;
  actual: string;
  steps: string;
  logs: string;
  demo: boolean;
};
export type Service = {
  id: string;
  name: string;
  bundled: boolean;
  gitHubRepository: string | null;
};
export type Configuration = {
  modelConfigured: boolean;
  runnerEnabled: boolean;
  githubConfigured: boolean;
  services: Service[];
};
export type Summary = {
  id: string;
  createdAt: string;
  status: string;
  verification: string;
  title: string;
  serviceId: string;
  demo: boolean;
};
export type Run = {
  id: string;
  createdAt: string;
  status: string;
  verification: string;
  report: Report;
  revision: string | null;
  sourceDigest: string | null;
  error: string | null;
  review: string | null;
  reviewNotes: string | null;
  pullRequestUrl: string | null;
  diff: string;
  usage: { inputTokens: number; outputTokens: number } | null;
  proposal: {
    summary: string;
    rootCause: string;
    uncertainty: string;
    regressionTest: string;
    citations: {
      path: string;
      startLine: number;
      endLine: number;
      reason: string;
    }[];
  } | null;
  evidence: { path: string; content: string; score: number }[];
  events: { at: string; stage: string; message: string }[];
  executions: {
    stage: string;
    command: string;
    exitCode: number;
    timedOut: boolean;
    output: string;
    durationMs: number;
    protocolValid: boolean;
    passed: number;
    failed: number;
  }[];
};
export const demoReport: Report = {
  serviceId: "checkout-demo",
  title: "Checkout succeeds but the order stays Pending",
  description:
    "Successful payment returns an order that still has Pending status. Fetching that order returns the same incorrect state.",
  expected: "Successful payment should return and store a Paid order.",
  actual: "HTTP 200, but the order status is Pending.",
  steps:
    "POST /checkout with productId=book, quantity=1, paymentToken=success. Then GET /orders/{id}.",
  logs: "Checkout.Api: payment succeeded; order status=Pending",
  demo: true,
};
export function verificationLabel(value: string) {
  return (
    (
      {
        NotRun: "Not run",
        NotConfigured: "Runner not configured",
        BaselineFailed: "Baseline failed",
        NotReproduced: "Not reproduced",
        Passed: "Checks passed",
        Failed: "Checks failed",
      } as Record<string, string>
    )[value] ?? value
  );
}
