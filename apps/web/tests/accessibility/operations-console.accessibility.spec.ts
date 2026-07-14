import AxeBuilder from "@axe-core/playwright";
import { expect, Page, test } from "@playwright/test";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";

const evidenceDirectory = path.resolve(process.cwd(), "../../.backups/accessibility");
const wcagTags = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

const dashboard = {
  generatedAt: "2026-07-13T15:00:00.000Z",
  warehouse: { code: "WH01", name: "Almacén piloto Central" },
  metrics: { inboundPending: 7, putawayPending: 18, ordersAtRisk: 2, tasksActive: 3, stockBlocked: 4, devicesOffline: 1 },
  flow: [
    { stage: "ASN", count: 7, status: "normal" },
    { stage: "Staging", count: 18, status: "watch" },
    { stage: "Stock", count: 1420, status: "normal" },
    { stage: "Reserva", count: 26, status: "normal" },
    { stage: "Picking", count: 9, status: "blocked" },
    { stage: "Despacho", count: 4, status: "normal" },
  ],
  tasks: [{ id: "task-1", type: "PICK", reference: "SO-1001", assignee: "Operario 7", status: "IN_PROGRESS", priority: 90, updatedAt: "2026-07-13T14:58:00.000Z" }],
  integration: [{ messageId: "message-1", type: "ShipmentConfirmation", externalId: "SO-1001", status: "Retrying", attempts: 2, latencyMs: null, correlationId: "12345678-1234-1234-1234-123456789012" }],
  alerts: [{ id: "alert-1", severity: "critical", title: "Pedido en riesgo", detail: "La ventana de despacho vence en 20 minutos.", ageMinutes: 4 }],
};

type ScenarioEvidence = {
  scenario: string;
  viewport: { width: number; height: number };
  violations: Array<{ id: string; impact: string | null; help: string; nodes: number }>;
  passes: number;
  incomplete: number;
  screenshot: string;
};

async function mockDashboard(page: Page, response: "ready" | "empty" | "error") {
  await page.unroute("**/api/v1/supervisor/dashboard").catch(() => undefined);
  await page.route("**/api/v1/supervisor/dashboard", async (route) => {
    if (response === "error") {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ code: "DASHBOARD_UNAVAILABLE" }) });
      return;
    }

    const payload = response === "empty"
      ? { ...dashboard, metrics: { inboundPending: 0, putawayPending: 0, ordersAtRisk: 0, tasksActive: 0, stockBlocked: 0, devicesOffline: 0 }, tasks: [], integration: [], alerts: [] }
      : dashboard;
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(payload) });
  });
}

async function scanScenario(page: Page, scenario: string, response: "ready" | "empty" | "error", viewport: { width: number; height: number }): Promise<ScenarioEvidence> {
  await page.setViewportSize(viewport);
  await mockDashboard(page, response);
  await page.goto("/");

  if (response === "error") await expect(page.locator(".state-banner[role='alert']")).toContainText("No se cargó");
  else if (response === "empty") await expect(page.getByText("No hay tareas activas")).toBeVisible();
  else await expect(page.getByRole("heading", { level: 1, name: "Almacén piloto Central" })).toBeVisible();

  const analysis = await new AxeBuilder({ page }).withTags(wcagTags).analyze();
  const screenshot = `${scenario}.png`;
  await page.screenshot({ path: path.join(evidenceDirectory, screenshot), fullPage: true });

  return {
    scenario,
    viewport,
    violations: analysis.violations.map(({ id, impact, help, nodes }) => ({ id, impact: impact ?? null, help, nodes: nodes.length })),
    passes: analysis.passes.length,
    incomplete: analysis.incomplete.length,
    screenshot,
  };
}

test("TEST-UX-0001: consola operacional cumple el gate automatizado WCAG 2.2 AA", async ({ page }) => {
  await mkdir(evidenceDirectory, { recursive: true });
  const scenarios: ScenarioEvidence[] = [];

  scenarios.push(await scanScenario(page, "desktop-ready", "ready", { width: 1440, height: 1000 }));

  await page.keyboard.press("Home");
  await page.keyboard.press("Tab");
  const skipLink = page.getByRole("link", { name: "Saltar al contenido principal" });
  await expect(skipLink).toBeFocused();
  await expect(skipLink).toBeVisible();
  await page.keyboard.press("Enter");
  await expect(page.locator("#main-content")).toBeFocused();
  await page.keyboard.press("Tab");
  const focusedOutline = await page.locator(":focus").evaluate((element) => getComputedStyle(element).outlineStyle);
  const namedButtons = await page.getByRole("button").evaluateAll((buttons) => buttons.every((button) => (button.getAttribute("aria-label") ?? button.textContent ?? "").trim().length > 0));

  scenarios.push(await scanScenario(page, "desktop-empty", "empty", { width: 1440, height: 1000 }));
  scenarios.push(await scanScenario(page, "desktop-error", "error", { width: 1440, height: 1000 }));
  scenarios.push(await scanScenario(page, "mobile-ready", "ready", { width: 360, height: 800 }));

  const evidence = {
    testId: "TEST-UX-0001",
    standard: "WCAG 2.2 AA",
    engine: "axe-core 4.12.1",
    generatedAt: new Date().toISOString(),
    commit: process.env.GITHUB_SHA ?? "local",
    result: scenarios.every((scenario) => scenario.violations.length === 0) && focusedOutline !== "none" && namedButtons ? "PASS" : "FAIL",
    automatedScope: wcagTags,
    keyboard: { skipLink: true, mainContentFocus: true, visibleFocusIndicator: focusedOutline !== "none", allButtonsNamed: namedButtons },
    manualChecksPending: ["lector de pantalla", "zoom 200/400 %", "contraste en dispositivo real", "UAT física Zebra con guantes"],
    scenarios,
  };
  await writeFile(path.join(evidenceDirectory, "wms-accessibility-evidence.json"), `${JSON.stringify(evidence, null, 2)}\n`, "utf8");

  expect(evidence.keyboard).toEqual({ skipLink: true, mainContentFocus: true, visibleFocusIndicator: true, allButtonsNamed: true });
  expect(scenarios.flatMap((scenario) => scenario.violations), JSON.stringify(scenarios, null, 2)).toEqual([]);
});
