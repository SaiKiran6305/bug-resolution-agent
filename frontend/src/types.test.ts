import { describe, it, expect } from "vitest";
import { verificationLabel } from "./types";
describe("verification status communicates evidence limits", () => {
  it("does not label missing verification as success", () => {
    expect(verificationLabel("NotConfigured")).toBe("Runner not configured");
    expect(verificationLabel("NotReproduced")).toBe("Not reproduced");
  });
  it("keeps baseline failures distinguishable from patch failures", () => {
    expect(verificationLabel("BaselineFailed")).toBe("Baseline failed");
    expect(verificationLabel("Failed")).toBe("Checks failed");
  });
});
