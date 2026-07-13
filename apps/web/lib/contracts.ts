export type Signal = "normal" | "watch" | "blocked" | "offline";

export type Dashboard = {
  generatedAt: string;
  warehouse: { code: string; name: string };
  metrics: {
    inboundPending: number;
    putawayPending: number;
    ordersAtRisk: number;
    tasksActive: number;
    stockBlocked: number;
    devicesOffline: number;
  };
  flow: Array<{ stage: string; count: number; status: Signal }>;
  tasks: Array<{
    id: string;
    type: string;
    reference: string;
    assignee: string;
    status: string;
    priority: number;
    updatedAt: string;
  }>;
  integration: Array<{
    messageId: string;
    type: string;
    externalId: string;
    status: string;
    attempts: number;
    latencyMs: number | null;
    correlationId: string;
  }>;
  alerts: Array<{
    id: string;
    severity: "info" | "warning" | "critical";
    title: string;
    detail: string;
    ageMinutes: number;
  }>;
};

export const emptyDashboard: Dashboard = {
  generatedAt: new Date(0).toISOString(),
  warehouse: { code: "WH01", name: "Almacén piloto" },
  metrics: {
    inboundPending: 0,
    putawayPending: 0,
    ordersAtRisk: 0,
    tasksActive: 0,
    stockBlocked: 0,
    devicesOffline: 0,
  },
  flow: ["ASN", "Staging", "Stock", "Reserva", "Picking", "Despacho"].map((stage) => ({
    stage,
    count: 0,
    status: "normal" as const,
  })),
  tasks: [],
  integration: [],
  alerts: [],
};
