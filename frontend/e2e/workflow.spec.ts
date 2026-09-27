import { test, expect } from "@playwright/test";
test("developer can inspect and review a labeled demo", async ({ page }) => {
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "Connect to your workspace" }),
  ).toBeVisible();
  await page
    .getByLabel("Access key")
    .fill("browser-test-key-not-a-real-secret");
  await page.getByRole("button", { name: "Connect", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Let’s investigate the bug." }),
  ).toBeVisible();
  await page.screenshot({ path: "test-results/intake.png", fullPage: true });
  await page.getByRole("button", { name: "Run sample investigation" }).click();
  await expect(
    page.getByRole("button", { name: "Approve proposal" }),
  ).toBeVisible({ timeout: 15000 });
  await expect(
    page.getByText("Runner not configured", { exact: true }),
  ).toBeVisible();
  await page.getByRole("tab", { name: "Evidence", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Retrieved source" }),
  ).toBeVisible();
  await page.getByRole("tab", { name: "Patch", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Download patch" }),
  ).toBeEnabled();
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Download patch" }).click();
  expect((await download).suggestedFilename()).toMatch(/\.patch$/);
  await page.getByRole("tab", { name: /Tests/ }).click();
  await expect(
    page.getByText("No tests have run.", { exact: false }),
  ).toBeVisible();
  await page.getByRole("tab", { name: "Overview", exact: true }).click();
  await page
    .getByLabel("Review notes")
    .fill("Reviewed the deterministic sample. Verification is not configured.");
  await page.getByRole("button", { name: "Approve proposal" }).click();
  await expect(page.locator(".review-state")).toHaveText("Approved");
  await page.screenshot({ path: "test-results/review.png", fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(
    page.getByRole("heading", { name: "Developer review" }),
  ).toBeVisible();
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth),
  ).toBeLessThanOrEqual(390);
});
