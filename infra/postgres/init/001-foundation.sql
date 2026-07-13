\set ON_ERROR_STOP on

CREATE SCHEMA IF NOT EXISTS keycloak;

DO $$
DECLARE
  role_name text;
BEGIN
  FOREACH role_name IN ARRAY ARRAY[
    'wms_runtime',
    'wms_migrator',
    'wms_platform',
    'wms_tenancy',
    'wms_security',
    'wms_layout',
    'wms_master_data',
    'wms_inventory',
    'wms_inbound',
    'wms_outbound',
    'wms_task_execution',
    'wms_integration',
    'wms_mobile_sync'
  ]
  LOOP
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
      EXECUTE format('CREATE ROLE %I NOLOGIN', role_name);
    END IF;
  END LOOP;
END
$$;

DO $$
DECLARE
  item text[];
BEGIN
  FOREACH item SLICE 1 IN ARRAY ARRAY[
    ARRAY['platform', 'wms_platform'],
    ARRAY['tenancy', 'wms_tenancy'],
    ARRAY['security', 'wms_security'],
    ARRAY['layout', 'wms_layout'],
    ARRAY['master_data', 'wms_master_data'],
    ARRAY['inventory', 'wms_inventory'],
    ARRAY['inbound', 'wms_inbound'],
    ARRAY['outbound', 'wms_outbound'],
    ARRAY['task_execution', 'wms_task_execution'],
    ARRAY['integration', 'wms_integration'],
    ARRAY['mobile_sync', 'wms_mobile_sync']
  ]
  LOOP
    EXECUTE format('CREATE SCHEMA IF NOT EXISTS %I AUTHORIZATION %I', item[1], item[2]);
    EXECUTE format('REVOKE ALL ON SCHEMA %I FROM PUBLIC', item[1]);
    EXECUTE format('GRANT USAGE, CREATE ON SCHEMA %I TO %I', item[1], item[2]);
    EXECUTE format('GRANT %I TO wms_migrator', item[2]);
  END LOOP;
END
$$;

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
COMMENT ON ROLE wms_runtime IS 'Base group without module data access.';
COMMENT ON ROLE wms_migrator IS 'Migration group; deployment identity only.';
