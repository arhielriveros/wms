-- WMS MVP initial expand-only migration. Run as a dedicated migration role.
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE SCHEMA IF NOT EXISTS tenancy;
CREATE SCHEMA IF NOT EXISTS security_audit;
CREATE SCHEMA IF NOT EXISTS layout;
CREATE SCHEMA IF NOT EXISTS master_data;
CREATE SCHEMA IF NOT EXISTS inventory;
CREATE SCHEMA IF NOT EXISTS inbound;
CREATE SCHEMA IF NOT EXISTS outbound;
CREATE SCHEMA IF NOT EXISTS task_execution;
CREATE SCHEMA IF NOT EXISTS integration;
CREATE SCHEMA IF NOT EXISTS mobile_sync;

DO $$ BEGIN
  CREATE ROLE wms_api NOLOGIN;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE ROLE wms_worker NOLOGIN BYPASSRLS;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $roles$
DECLARE role_name text;
BEGIN
  FOREACH role_name IN ARRAY ARRAY[
    'wms_mod_tenancy', 'wms_mod_security_audit', 'wms_mod_layout', 'wms_mod_master_data',
    'wms_mod_inventory', 'wms_mod_inbound', 'wms_mod_outbound', 'wms_mod_task_execution',
    'wms_mod_integration', 'wms_mod_mobile_sync'
  ]
  LOOP
    BEGIN
      EXECUTE format('CREATE ROLE %I NOLOGIN', role_name);
    EXCEPTION WHEN duplicate_object THEN NULL;
    END;
  END LOOP;
END $roles$;

CREATE TABLE IF NOT EXISTS tenancy.tenant (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, code varchar(64) NOT NULL UNIQUE, name varchar(200) NOT NULL, is_active boolean NOT NULL
);
CREATE TABLE IF NOT EXISTS security_audit.audit_record (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL CHECK (version = 1), actor_id text NOT NULL, action text NOT NULL,
  object_type text NOT NULL, object_id text NOT NULL, result text NOT NULL, reason_code text NULL, metadata jsonb NOT NULL
);
CREATE TABLE IF NOT EXISTS layout.warehouse (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, code text NOT NULL, name text NOT NULL, is_active boolean NOT NULL,
  UNIQUE (tenant_id, code)
);
CREATE TABLE IF NOT EXISTS layout.location (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, warehouse_id uuid NOT NULL, code text NOT NULL,
  zone_code text NOT NULL, type text NOT NULL, is_active boolean NOT NULL, UNIQUE (tenant_id, warehouse_id, code)
);
CREATE TABLE IF NOT EXISTS master_data.owner (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, code text NOT NULL, name text NOT NULL, UNIQUE (tenant_id, code)
);
CREATE TABLE IF NOT EXISTS master_data.sku (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, owner_id uuid NOT NULL, code text NOT NULL, description text NOT NULL,
  uom text NOT NULL, barcode text NOT NULL, UNIQUE (tenant_id, owner_id, code)
);
CREATE TABLE IF NOT EXISTS inventory.stock_balance (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, warehouse_id uuid NOT NULL, owner_id uuid NOT NULL, sku varchar(100) NOT NULL,
  location_id uuid NOT NULL, status varchar(32) NOT NULL, on_hand numeric(18,4) NOT NULL, reserved numeric(18,4) NOT NULL,
  blocked numeric(18,4) NOT NULL, received_at timestamptz NOT NULL, last_movement_id uuid NOT NULL,
  CHECK (on_hand >= 0 AND reserved >= 0 AND blocked >= 0), CHECK (reserved + blocked <= on_hand),
  UNIQUE (tenant_id, warehouse_id, owner_id, sku, location_id, status)
);
CREATE TABLE IF NOT EXISTS inventory.movement (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, stock_dimension_id uuid NOT NULL, related_stock_dimension_id uuid NULL, movement_type text NOT NULL,
  quantity numeric(18,4) NOT NULL, uom text NOT NULL, command_id uuid NOT NULL, payload_checksum text NOT NULL,
  compensates_movement_id uuid NULL, occurred_at timestamptz NOT NULL, UNIQUE (tenant_id, command_id)
);
CREATE TABLE IF NOT EXISTS inventory.reservation (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, stock_dimension_id uuid NOT NULL, order_id uuid NOT NULL,
  order_line_id uuid NOT NULL, quantity numeric(18,4) NOT NULL, consumed_quantity numeric(18,4) NOT NULL,
  released_quantity numeric(18,4) NOT NULL, status text NOT NULL, command_id uuid NOT NULL, payload_checksum text NOT NULL,
  UNIQUE (tenant_id, command_id, stock_dimension_id)
);
CREATE TABLE IF NOT EXISTS inbound.asn (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, source_message_id uuid NOT NULL, external_id text NOT NULL,
  warehouse_code text NOT NULL, owner_code text NOT NULL, supplier_external_id text NULL, expected_at timestamptz NOT NULL,
  status text NOT NULL, UNIQUE (tenant_id, source_message_id), UNIQUE (tenant_id, warehouse_code, owner_code, external_id)
);
CREATE TABLE IF NOT EXISTS inbound.asn_line (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, asn_id uuid NOT NULL REFERENCES inbound.asn(id), external_line_id text NOT NULL,
  sku text NOT NULL, expected_quantity numeric(18,4) NOT NULL, received_quantity numeric(18,4) NOT NULL,
  putaway_quantity numeric(18,4) NOT NULL, uom text NOT NULL
);
CREATE TABLE IF NOT EXISTS inbound.receipt (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, asn_id uuid NOT NULL, completed_at timestamptz NULL, status text NOT NULL
);
CREATE TABLE IF NOT EXISTS outbound.sales_order (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, source_message_id uuid NOT NULL, external_id text NOT NULL,
  warehouse_code text NOT NULL, owner_code text NOT NULL, customer_external_id text NULL, priority integer NOT NULL,
  requested_ship_at timestamptz NOT NULL, status text NOT NULL, UNIQUE (tenant_id, source_message_id),
  UNIQUE (tenant_id, warehouse_code, owner_code, external_id)
);
CREATE TABLE IF NOT EXISTS outbound.order_line (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, sales_order_id uuid NOT NULL REFERENCES outbound.sales_order(id),
  external_line_id text NOT NULL, sku text NOT NULL, ordered_quantity numeric(18,4) NOT NULL,
  allocated_quantity numeric(18,4) NOT NULL, picked_quantity numeric(18,4) NOT NULL,
  short_picked_quantity numeric(18,4) NOT NULL DEFAULT 0, short_pick_reason text NULL, uom text NOT NULL
);
CREATE TABLE IF NOT EXISTS outbound.shipment (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, sales_order_id uuid NOT NULL, status text NOT NULL, dispatched_at timestamptz NULL
);
CREATE TABLE IF NOT EXISTS task_execution.task (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, warehouse_id uuid NOT NULL, type text NOT NULL, reference text NOT NULL,
  owner_entity_id uuid NOT NULL, assignee_id text NULL, device_id text NULL, zone_code text NULL, expires_at timestamptz NULL,
  status text NOT NULL, priority integer NOT NULL, updated_at timestamptz NOT NULL
);
CREATE TABLE IF NOT EXISTS task_execution.task_step (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, task_id uuid NOT NULL REFERENCES task_execution.task(id), sequence integer NOT NULL,
  action text NOT NULL, location_barcode text NULL, sku_barcode text NULL, quantity numeric(18,4) NOT NULL, uom text NOT NULL,
  completed boolean NOT NULL
);
CREATE TABLE IF NOT EXISTS integration.inbox (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, message_id uuid NOT NULL, source_system text NOT NULL,
  message_type text NOT NULL, schema_version text NOT NULL, payload_checksum text NOT NULL, payload jsonb NOT NULL,
  status text NOT NULL, error_code text NULL, UNIQUE (tenant_id, source_system, message_type, message_id)
);
CREATE TABLE IF NOT EXISTS integration.outbox (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, message_id uuid NOT NULL, message_type text NOT NULL,
  schema_version text NOT NULL, source_system text NOT NULL, causation_id uuid NULL, payload jsonb NOT NULL,
  delivery_kind text NOT NULL, destination text NULL, status text NOT NULL, attempts integer NOT NULL,
  next_attempt_at timestamptz NULL, last_error_code text NULL, delivered_at timestamptz NULL, UNIQUE (tenant_id, message_id)
);
CREATE TABLE IF NOT EXISTS integration.delivery_attempt (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, outbox_message_id uuid NOT NULL, attempt_number integer NOT NULL,
  started_at timestamptz NOT NULL, completed_at timestamptz NULL, http_status integer NULL, result text NOT NULL, error_code text NULL
);
CREATE TABLE IF NOT EXISTS mobile_sync.device (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, device_id text NOT NULL, warehouse_id uuid NOT NULL,
  status text NOT NULL, last_seen_at timestamptz NOT NULL, UNIQUE (tenant_id, device_id)
);
CREATE TABLE IF NOT EXISTS mobile_sync.command_inbox (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, command_id uuid NOT NULL, task_id uuid NOT NULL,
  local_sequence bigint NOT NULL, payload_checksum text NOT NULL, result_status text NOT NULL, result_code text NOT NULL,
  result_message text NOT NULL, current_version bigint NULL, suggested_action text NOT NULL, UNIQUE (tenant_id, command_id)
);
CREATE TABLE IF NOT EXISTS mobile_sync.sync_checkpoint (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL, created_by text NOT NULL,
  correlation_id uuid NOT NULL, version bigint NOT NULL, device_id text NOT NULL, user_id text NOT NULL,
  value bigint NOT NULL, updated_at timestamptz NOT NULL, UNIQUE (tenant_id, device_id, user_id)
);

-- Fail closed for API sessions. The API must SET LOCAL app.tenant_id in every transaction.
DO $rls$
DECLARE r record;
BEGIN
  FOR r IN SELECT schemaname, tablename FROM pg_tables
           WHERE schemaname IN ('tenancy','security_audit','layout','master_data','inventory','inbound','outbound','task_execution','integration','mobile_sync')
  LOOP
    EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY', r.schemaname, r.tablename);
    EXECUTE format('ALTER TABLE %I.%I FORCE ROW LEVEL SECURITY', r.schemaname, r.tablename);
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.schemaname, r.tablename);
    EXECUTE format('CREATE POLICY tenant_isolation ON %I.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', r.schemaname, r.tablename);
  END LOOP;
END $rls$;

GRANT USAGE ON SCHEMA tenancy TO wms_mod_tenancy;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA tenancy TO wms_mod_tenancy;
GRANT USAGE ON SCHEMA security_audit TO wms_mod_security_audit;
GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA security_audit TO wms_mod_security_audit;
GRANT USAGE ON SCHEMA layout TO wms_mod_layout;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA layout TO wms_mod_layout;
GRANT USAGE ON SCHEMA master_data TO wms_mod_master_data;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA master_data TO wms_mod_master_data;
GRANT USAGE ON SCHEMA inventory TO wms_mod_inventory;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA inventory TO wms_mod_inventory;
GRANT USAGE ON SCHEMA inbound TO wms_mod_inbound;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA inbound TO wms_mod_inbound;
GRANT USAGE ON SCHEMA outbound TO wms_mod_outbound;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA outbound TO wms_mod_outbound;
GRANT USAGE ON SCHEMA task_execution TO wms_mod_task_execution;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA task_execution TO wms_mod_task_execution;
GRANT USAGE ON SCHEMA integration TO wms_mod_integration;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA integration TO wms_mod_integration;
GRANT USAGE ON SCHEMA mobile_sync TO wms_mod_mobile_sync;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA mobile_sync TO wms_mod_mobile_sync;

GRANT wms_mod_tenancy, wms_mod_security_audit, wms_mod_layout, wms_mod_master_data,
      wms_mod_inventory, wms_mod_inbound, wms_mod_outbound, wms_mod_task_execution,
      wms_mod_integration, wms_mod_mobile_sync TO wms_api;
GRANT wms_mod_integration, wms_mod_inbound, wms_mod_outbound, wms_mod_layout,
      wms_mod_master_data, wms_mod_task_execution TO wms_worker;

REVOKE UPDATE, DELETE ON inventory.movement FROM wms_mod_inventory, wms_api, wms_worker;
REVOKE UPDATE, DELETE ON security_audit.audit_record FROM wms_mod_security_audit, wms_api, wms_worker;
