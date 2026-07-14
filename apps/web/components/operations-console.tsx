"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { loadDashboard } from "@/lib/api";
import { Dashboard, emptyDashboard, Signal } from "@/lib/contracts";

type ViewState = "loading" | "ready" | "error" | "offline";

const navigation = ["Operación", "Inventario", "Entradas", "Salidas", "Integraciones", "Dispositivos"];

const signalLabel: Record<Signal, string> = {
  normal: "En flujo",
  watch: "Atención",
  blocked: "Bloqueado",
  offline: "Sin enlace",
};

function shortTime(value: string) {
  return new Intl.DateTimeFormat("es-PY", { hour: "2-digit", minute: "2-digit" }).format(new Date(value));
}

export function OperationsConsole() {
  const [dashboard, setDashboard] = useState<Dashboard>(emptyDashboard);
  const [viewState, setViewState] = useState<ViewState>("loading");
  const [activeNav, setActiveNav] = useState("Operación");
  const [lastError, setLastError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    if (!navigator.onLine) {
      setViewState("offline");
      return;
    }
    setViewState((current) => (current === "ready" ? current : "loading"));
    try {
      const value = await loadDashboard();
      setDashboard(value);
      setLastError(null);
      setViewState("ready");
    } catch (error) {
      setLastError(error instanceof Error ? error.message : "DASHBOARD_UNAVAILABLE");
      setViewState("error");
    }
  }, []);

  useEffect(() => {
    void refresh();
    const online = () => void refresh();
    const offline = () => setViewState("offline");
    window.addEventListener("online", online);
    window.addEventListener("offline", offline);
    const timer = window.setInterval(() => void refresh(), 30_000);
    return () => {
      window.removeEventListener("online", online);
      window.removeEventListener("offline", offline);
      window.clearInterval(timer);
    };
  }, [refresh]);

  const operationalRisk = useMemo(
    () => dashboard.metrics.ordersAtRisk + dashboard.metrics.stockBlocked + dashboard.metrics.devicesOffline,
    [dashboard.metrics],
  );

  return (
    <main className="app-shell">
      <a className="skip-link" href="#main-content">Saltar al contenido principal</a>
      <aside className="side-rail" aria-label="Navegación principal">
        <div className="brand-lockup" aria-label="WMS Control">
          <span className="brand-mark">W</span>
          <span>WMS<br /><small>CONTROL</small></span>
        </div>
        <nav>
          {navigation.map((item) => (
            <button
              className={activeNav === item ? "nav-item active" : "nav-item"}
              key={item}
              onClick={() => setActiveNav(item)}
              aria-current={activeNav === item ? "page" : undefined}
            >
              <span className="nav-notch" aria-hidden="true" />
              {item}
            </button>
          ))}
        </nav>
        <div className="rail-footer">
          <span className="live-dot" aria-hidden="true" /> API / Worker
          <small>v0.1 · WH01</small>
        </div>
      </aside>

      <section className="workspace" id="main-content" tabIndex={-1}>
        <header className="command-bar">
          <div>
            <p className="eyebrow">{dashboard.warehouse.code} · TURNO ACTIVO</p>
            <h1>{dashboard.warehouse.name}</h1>
          </div>
          <div className="command-actions">
            <div className={`connection-chip ${viewState}`} role="status" aria-live="polite">
              {viewState === "offline" ? "Sin conexión" : viewState === "error" ? "Datos no disponibles" : "Enlace operativo"}
            </div>
            <button className="refresh-button" onClick={() => void refresh()}>Actualizar</button>
            <div className="avatar" role="img" aria-label="Supervisor AR">AR</div>
          </div>
        </header>

        {viewState === "loading" && <LoadingPanel />}
        {viewState === "offline" && (
          <StateBanner tone="warning" title="Consola sin conexión" detail="Se conserva la última lectura. La ejecución móvil continúa con tareas descargadas." action="Reintentar" onAction={refresh} />
        )}
        {viewState === "error" && (
          <StateBanner tone="danger" title="No se cargó el estado del almacén" detail={`La API no respondió. Código: ${lastError ?? "DASHBOARD_UNAVAILABLE"}.`} action="Reintentar" onAction={refresh} />
        )}

        <section className="flow-section" aria-labelledby="flow-title">
          <div className="section-heading">
            <div>
              <p className="eyebrow">RIEL OPERACIONAL</p>
              <h2 id="flow-title">Del documento al muelle</h2>
            </div>
            <p><strong>{operationalRisk}</strong> señales requieren revisión</p>
          </div>
          <div className="flow-rail">
            {dashboard.flow.map((item, index) => (
              <article className={`flow-stop ${item.status}`} key={item.stage}>
                <span className="flow-index">{String(index + 1).padStart(2, "0")}</span>
                <strong>{item.count}</strong>
                <h3>{item.stage}</h3>
                <span className="signal"><i aria-hidden="true" />{signalLabel[item.status]}</span>
              </article>
            ))}
          </div>
        </section>

        <section className="metric-strip" aria-label="Indicadores operacionales">
          <Metric label="Recepciones pendientes" value={dashboard.metrics.inboundPending} hint="ASN abiertos" />
          <Metric label="Putaway pendiente" value={dashboard.metrics.putawayPending} hint="unidades en staging" />
          <Metric label="Pedidos en riesgo" value={dashboard.metrics.ordersAtRisk} hint="ventana SLA" danger={dashboard.metrics.ordersAtRisk > 0} />
          <Metric label="Tareas activas" value={dashboard.metrics.tasksActive} hint="operarios asignados" />
          <Metric label="Stock bloqueado" value={dashboard.metrics.stockBlocked} hint="unidades" danger={dashboard.metrics.stockBlocked > 0} />
          <Metric label="Dispositivos offline" value={dashboard.metrics.devicesOffline} hint="requieren sync" danger={dashboard.metrics.devicesOffline > 0} />
        </section>

        <section className="operations-grid">
          <div className="panel tasks-panel">
            <PanelTitle eyebrow="EJECUCIÓN" title="Tareas en movimiento" action="Ver todas" />
            {dashboard.tasks.length === 0 ? (
              <EmptyState title="No hay tareas activas" detail="Libera un ASN o pedido para crear trabajo asignable." />
            ) : (
              <div className="table-wrap" role="region" aria-label="Tareas activas; desplazar horizontalmente para ver todas las columnas" tabIndex={0}>
                <table>
                  <caption className="sr-only">Tareas activas del turno</caption>
                  <thead><tr><th>Tipo</th><th>Referencia</th><th>Asignado</th><th>Prioridad</th><th>Estado</th><th>Actualizado</th></tr></thead>
                  <tbody>
                    {dashboard.tasks.slice(0, 7).map((task) => (
                      <tr key={task.id}>
                        <td><span className="task-code">{task.type}</span></td>
                        <td><strong>{task.reference}</strong></td>
                        <td>{task.assignee || "Sin asignar"}</td>
                        <td><span className={task.priority >= 80 ? "priority high" : "priority"}>{task.priority}</span></td>
                        <td><span className="status-badge"><i />{task.status}</span></td>
                        <td>{shortTime(task.updatedAt)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <div className="panel alerts-panel">
            <PanelTitle eyebrow="EXCEPCIONES" title="Lo que cambia el turno" />
            {dashboard.alerts.length === 0 ? (
              <EmptyState title="Operación estable" detail="No hay excepciones abiertas en esta ventana." />
            ) : dashboard.alerts.slice(0, 5).map((alert) => (
              <article className={`alert-row ${alert.severity}`} key={alert.id}>
                <span className="alert-symbol" aria-hidden="true">!</span>
                <div><strong>{alert.title}</strong><p>{alert.detail}</p></div>
                <time>{alert.ageMinutes}m</time>
              </article>
            ))}
          </div>

          <div className="panel integration-panel">
            <PanelTitle eyebrow="ERP · REST" title="Pulso de integración" action="Abrir consola" />
            {dashboard.integration.length === 0 ? (
              <EmptyState title="Sin mensajes recientes" detail="Los ASN y pedidos entrantes aparecerán con su correlación." />
            ) : dashboard.integration.slice(0, 6).map((message) => (
              <article className="message-row" key={message.messageId}>
                <span className={`message-state ${message.status.toLowerCase()}`} aria-hidden="true" />
                <div><strong>{message.externalId}</strong><small>{message.type} · {message.correlationId.slice(0, 8)}</small></div>
                <span>{message.attempts} int.</span>
                <strong>{message.latencyMs === null ? "—" : `${message.latencyMs} ms`}</strong>
                <span className="sr-only">Estado: {message.status}</span>
              </article>
            ))}
          </div>
        </section>

        <footer className="workspace-footer">
          <span>Última lectura {dashboard.generatedAt === new Date(0).toISOString() ? "sin datos" : shortTime(dashboard.generatedAt)}</span>
          <span>Tenant aislado · Correlation ID en cada operación</span>
        </footer>
      </section>
    </main>
  );
}

function Metric({ label, value, hint, danger = false }: { label: string; value: number; hint: string; danger?: boolean }) {
  return <article className={danger ? "metric danger" : "metric"}><span>{label}</span><strong>{value.toLocaleString("es-PY")}</strong><small>{hint}</small></article>;
}

function PanelTitle({ eyebrow, title, action }: { eyebrow: string; title: string; action?: string }) {
  return <div className="panel-title"><div><p className="eyebrow">{eyebrow}</p><h2>{title}</h2></div>{action && <button>{action}</button>}</div>;
}

function EmptyState({ title, detail }: { title: string; detail: string }) {
  return <div className="empty-state"><span aria-hidden="true">□</span><div><strong>{title}</strong><p>{detail}</p></div></div>;
}

function LoadingPanel() {
  return <div className="loading-panel" role="status"><span className="loader" aria-hidden="true" /><div><strong>Reconstruyendo el turno</strong><p>Consultando tareas, stock e integraciones.</p></div></div>;
}

function StateBanner({ tone, title, detail, action, onAction }: { tone: "warning" | "danger"; title: string; detail: string; action: string; onAction: () => void }) {
  return <div className={`state-banner ${tone}`} role="alert"><div><strong>{title}</strong><p>{detail}</p></div><button onClick={onAction}>{action}</button></div>;
}
