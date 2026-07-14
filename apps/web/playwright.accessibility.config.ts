import { defineConfig } from "@playwright/test";

const port = Number(process.env.WMS_ACCESSIBILITY_PORT ?? 3100);
const externalBaseURL = process.env.WMS_ACCESSIBILITY_BASE_URL;
const baseURL = externalBaseURL ?? `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: "./tests/accessibility",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 300_000,
  expect: { timeout: 8_000 },
  reporter: [["line"]],
  outputDir: "../../.backups/accessibility/playwright-results",
  use: {
    baseURL,
    browserName: "chromium",
    colorScheme: "light",
    locale: "es-PY",
    trace: "off",
  },
  webServer: externalBaseURL ? undefined : {
    command: `node node_modules/next/dist/bin/next start --hostname 127.0.0.1 --port ${port}`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
