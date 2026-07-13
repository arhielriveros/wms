import http from "k6/http";
import { check } from "k6";

const baseUrl = __ENV.WMS_API_BASE_URL || "http://localhost:8081";
const tenantId = "11111111-1111-1111-1111-111111111111";
const warehouseId = "22222222-2222-2222-2222-222222222222";
const runSeed = Number(__ENV.RUN_SEED || Date.now()) % 0xffffff;

export const options = {
  scenarios: {
    concurrent_devices: {
      executor: "constant-vus",
      vus: 100,
      duration: "60s",
      exec: "deviceReads",
      gracefulStop: "10s",
    },
    command_batch: {
      executor: "per-vu-iterations",
      vus: 1,
      iterations: 3,
      maxDuration: "45s",
      exec: "batchCommands",
      startTime: "5s",
    },
  },
  thresholds: {
    "http_req_duration{scenario:concurrent_devices}": ["p(95)<500"],
    "http_req_duration{scenario:command_batch}": ["p(95)<10000"],
    http_req_failed: ["rate<0.01"],
  },
};

function headers(deviceId) {
  return {
    "Content-Type": "application/json",
    "X-Tenant-Id": tenantId,
    "X-Warehouse-Id": warehouseId,
    "X-Device-Id": deviceId,
    "X-User-Id": "operator-01",
  };
}

function uuid(sequence, variant = "4") {
  const suffix = Number(sequence).toString(16).padStart(12, "0").slice(-12);
  return `00000000-0000-${variant}000-8000-${suffix}`;
}

export function deviceReads() {
  const response = http.get(`${baseUrl}/api/v1/mobile/tasks?since=0`, {
    headers: headers(`load-device-${__VU}`),
    tags: { operation: "assigned_tasks" },
  });
  check(response, { "task read succeeds": (r) => r.status === 200 });
}

export function batchCommands() {
  // Keep command/task IDs unique between local and CI reruns so Inbox
  // idempotency does not turn a performance test into a cache hit.
  const baseSequence = runSeed * 100000 + __ITER * 1000;
  const commands = Array.from({ length: 100 }, (_, index) => ({
    commandId: uuid(baseSequence + index),
    commandType: "StartTask",
    schemaVersion: "1.0",
    tenantId,
    warehouseId,
    deviceId: "load-batch-device",
    userId: "operator-01",
    occurredAt: new Date().toISOString(),
    localSequence: index + 1,
    entityVersion: 1,
    taskId: uuid(baseSequence + index, "5"),
    payload: {},
  }));
  const response = http.post(`${baseUrl}/api/v1/mobile/commands:batch`, JSON.stringify({ commands }), {
    headers: headers("load-batch-device"),
    tags: { operation: "command_batch" },
    timeout: "10s",
  });
  check(response, {
    "batch returns all results": (r) => r.status === 200 && JSON.parse(r.body).results.length === 100,
  });
}
