import { Dashboard, emptyDashboard } from "./contracts";

const apiUrl = process.env.NEXT_PUBLIC_WMS_API_URL ?? "http://localhost:5080";
const tenantId = process.env.NEXT_PUBLIC_DEMO_TENANT_ID ?? "11111111-1111-1111-1111-111111111111";
const warehouseId = process.env.NEXT_PUBLIC_DEMO_WAREHOUSE_ID ?? "22222222-2222-2222-2222-222222222222";

export async function loadDashboard(signal?: AbortSignal): Promise<Dashboard> {
  const response = await fetch(`${apiUrl}/api/v1/supervisor/dashboard`, {
    signal,
    cache: "no-store",
    headers: {
      "X-Tenant-Id": tenantId,
      "X-Warehouse-Id": warehouseId,
      "X-Correlation-Id": crypto.randomUUID(),
    },
  });

  if (!response.ok) {
    throw new Error(`DASHBOARD_${response.status}`);
  }

  const payload = (await response.json()) as Partial<Dashboard>;
  return {
    ...emptyDashboard,
    ...payload,
    metrics: { ...emptyDashboard.metrics, ...payload.metrics },
  };
}
