# Consola web WMS

Consola Next.js para supervisión del flujo inbound/outbound. La interfaz consume exclusivamente la API versionada y conserva estados explícitos de carga, vacío, error y desconexión.

```powershell
Copy-Item .env.example .env.local
npm install
npm run dev
```

La API local esperada es `http://localhost:5080`. Los headers de tenant/warehouse del `.env.local` son sólo para desarrollo; en ambientes reales se derivan del token OIDC y son validados por el backend.
