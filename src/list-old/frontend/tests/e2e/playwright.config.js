import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.BASE_URL || "http://localhost:8080";

export default defineConfig({
  testDir: "./specs",
  // Tests mutate shared FHIR server state and there's no per-test data reset, so run
  // everything serially in a single worker to avoid cross-file/cross-test races.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? "line" : "html",
  use: {
    baseURL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  // In CI/Docker, BASE_URL points at an already-running `list` container, so Playwright
  // shouldn't try to start anything itself. Locally, spin up the Vite dev server for
  // convenience -- `KEYCLOAK_DISABLED` matches the value set for the `list` service in
  // tests/e2e/docker-compose.yaml so both paths run against the same unauthenticated view.
  webServer: process.env.BASE_URL
    ? undefined
    : {
        command: "npm run serve",
        cwd: "../..",
        url: baseURL,
        reuseExistingServer: !process.env.CI,
        timeout: 60_000,
      },
});
