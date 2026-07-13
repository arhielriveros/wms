\set ON_ERROR_STOP on
\timing on

BEGIN;
SET LOCAL synchronous_commit = off;

INSERT INTO inventory.stock_balance (
  id, tenant_id, created_at, created_by, correlation_id, version,
  warehouse_id, owner_id, sku, location_id, status,
  on_hand, reserved, blocked, received_at, last_movement_id
) VALUES (
  '77777777-7777-4777-8777-777777777777',
  '11111111-1111-1111-1111-111111111111',
  now(), 'historical-drill', '70000000-0000-4000-8000-000000000001', 1,
  '22222222-2222-2222-2222-222222222222',
  '33333333-3333-3333-3333-333333333333',
  'SKU-HISTORY', '55555555-5555-5555-5555-555555555555', 'Available',
  0, 0, 0, timestamp with time zone '2025-01-01 00:00:00+00',
  '70000000-0000-4000-8000-000000000002'
) ON CONFLICT (id) DO NOTHING;

INSERT INTO inventory.movement (
  id, tenant_id, created_at, created_by, correlation_id, version,
  stock_dimension_id, related_stock_dimension_id, movement_type,
  quantity, uom, command_id, payload_checksum, compensates_movement_id, occurred_at
)
SELECT
  ('70000000-0000-4000-8000-' || lpad(to_hex(series_id), 12, '0'))::uuid,
  '11111111-1111-1111-1111-111111111111'::uuid,
  timestamp with time zone '2025-01-01 00:00:00+00' + series_id * interval '1 second',
  'historical-drill', '70000000-0000-4000-8000-000000000001'::uuid, 1,
  '77777777-7777-4777-8777-777777777777'::uuid, NULL,
  CASE WHEN series_id % 2 = 0 THEN 'Receipt' ELSE 'Pick' END,
  CASE WHEN series_id % 2 = 0 THEN 1 ELSE -1 END,
  'EA',
  ('71000000-0000-4000-8000-' || lpad(to_hex(series_id), 12, '0'))::uuid,
  repeat('0', 64), NULL,
  timestamp with time zone '2025-01-01 00:00:00+00' + series_id * interval '1 second'
FROM generate_series(1, :movement_count) AS generated(series_id)
ON CONFLICT (tenant_id, command_id) DO NOTHING;

COMMIT;
ANALYZE inventory.movement;
