-- Deterministic resources for TEST-SEC-0002 IDOR checks. Security-drill only.

INSERT INTO outbound.sales_order (
  id, tenant_id, created_at, created_by, correlation_id, version, source_message_id,
  external_id, warehouse_code, owner_code, customer_external_id, priority, requested_ship_at, status
) VALUES
  ('71000000-0000-4000-8000-000000000001', '11111111-1111-1111-1111-111111111111', now(), 'security-seed', '73000000-0000-4000-8000-000000000001', 1, '72000000-0000-4000-8000-000000000001', 'SEC-ORDER-A', 'WH01', 'OWNER01', 'SEC-CUSTOMER-A', 1, now() + interval '1 day', 'Imported'),
  ('71000000-0000-4000-8000-000000000002', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now(), 'security-seed', '73000000-0000-4000-8000-000000000002', 1, '72000000-0000-4000-8000-000000000002', 'SEC-ORDER-B', 'WH01', 'OWNER01', 'SEC-CUSTOMER-B', 1, now() + interval '1 day', 'Imported')
ON CONFLICT (tenant_id, warehouse_code, owner_code, external_id) DO NOTHING;
