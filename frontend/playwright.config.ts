import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./e2e",
  workers: 1,
  use: {
    baseURL: "http://127.0.0.1:5081",
    viewport: { width: 1440, height: 1100 },
    screenshot: "only-on-failure",
  },
  webServer: {
    command: "python3 ../scripts/serve-e2e.py",
    url: "http://127.0.0.1:5081/health",
    timeout: 30000,
    reuseExistingServer: false,
  },
});
