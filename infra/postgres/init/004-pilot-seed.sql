-- Deterministic development fixtures for tenant-isolation and E2E smoke tests.
-- Never load this file in a production database.

INSERT INTO tenancy.tenant (id, tenant_id, created_at, created_by, correlation_id, version, code, name, is_active) VALUES
  ('11111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111', now(), 'seed', '10000000-0000-0000-0000-000000000001', 1, 'TENANT-A', 'Piloto A', true),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now(), 'seed', '10000000-0000-0000-0000-000000000002', 1, 'TENANT-B', 'Piloto B', true)
ON CONFLICT (id) DO NOTHING;

INSERT INTO layout.warehouse (id, tenant_id, created_at, created_by, correlation_id, version, code, name, is_active) VALUES
  ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', now(), 'seed', '10000000-0000-0000-0000-000000000001', 1, 'WH01', 'Almacén Piloto A', true),
  ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now(), 'seed', '10000000-0000-0000-0000-000000000002', 1, 'WH01', 'Almacén Piloto B', true)
ON CONFLICT (id) DO NOTHING;

INSERT INTO layout.location (id, tenant_id, created_at, created_by, correlation_id, version, warehouse_id, code, zone_code, type, is_active) VALUES
  ('44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', now(), 'seed', '10000000-0000-0000-0000-000000000001', 1, '22222222-2222-2222-2222-222222222222', 'STG-01', 'INBOUND', 'Staging', true),
  ('55555555-5555-5555-5555-555555555555', '11111111-1111-1111-1111-111111111111', now(), 'seed', '10000000-0000-0000-0000-000000000001', 1, '22222222-2222-2222-2222-222222222222', 'A-01-01', 'PICK-A', 'Storage', true),
  ('dddddddd-dddd-dddd-dddd-dddddddddddd', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now(), 'seed', '10000000-0000-0000-0000-000000000002', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'STG-01', 'INBOUND', 'Staging', true),
  ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now(), 'seed', '10000000-0000-0000-0000-000000000002', 1, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'A-01-01', 'PICK-A', 'Storage', true)
ON CONFLICT (id) DO NOTHING;

INSERT INTO master_data.owner (id, tenant_id, created_at, created_by, correlation_id, version, code, name) VALUES
  ('33333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111', now(), 'seed', '10000000-0000-0000-0000-000000000001', 1, 'OWNER01', 'Propietario Piloto A'),
  ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now(), 'seed', '10000000-0000-0000-0000-000000000002', 1, 'OWNER01', 'Propietario Piloto B')
ON CONFLICT (id) DO NOTHING;

INSERT INTO master_data.sku (id, tenant_id, created_at, created_by, correlation_id, version, owner_id, code, description, uom, barcode) VALUES
  ('66666666-6666-6666-6666-666666666666', '11111111-1111-1111-1111-111111111111', now(), 'seed', '10000000-0000-0000-0000-000000000001', 1, '33333333-3333-3333-3333-333333333333', 'SKU-001', 'SKU piloto A', 'EA', '784000000001'),
  ('ffffffff-ffff-ffff-ffff-ffffffffffff', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', now(), 'seed', '10000000-0000-0000-0000-000000000002', 1, 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'SKU-001', 'SKU piloto B', 'EA', '784000000001')
ON CONFLICT (id) DO NOTHING;
