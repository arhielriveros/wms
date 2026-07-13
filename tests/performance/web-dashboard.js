import http from "k6/http";
import { check } from "k6";

// TEST-OPS-0002: 30 concurrent supervisors against the operational projection.

const baseUrl = __ENV.WMS_API_BASE_URL || "http://localhost:8081";
const tenantId = "11111111-1111-1111-1111-111111111111";
const warehouseId = "22222222-2222-2222-2222-222222222222";

export const options = {
  scenarios: {
    supervisor_dashboard: {
      executor: "constant-vus",
      vus: 30,
      duration: "60s",
      gracefulStop: "10s",
    },
  },
  thresholds: {
    http_req_duration: ["p(95)<500"],
    http_req_failed: ["rate<0.01"],
    checks: ["rate>0.99"],
  },
};

export default function dashboardRefresh() {
  const response = http.get(`${baseUrl}/api/v1/supervisor/dashboard`, {
    headers: {
      "X-Tenant-Id": tenantId,
      "X-Warehouse-Id": warehouseId,
      "X-Correlation-Id": `00000000-0000-4000-8000-${String(__VU).padStart(12, "0")}`,
      "X-User-Id": `supervisor-${__VU}`,
    },
    tags: { operation: "supervisor_dashboard" },
  });
  check(response, {
    "dashboard returns operational projection": (result) => {
      if (result.status !== 200) return false;
      const payload = JSON.parse(result.body);
      return payload.generatedAt && payload.metrics && Array.isArray(payload.flow) && Array.isArray(payload.tasks);
    },
  });
}
