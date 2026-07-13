#!/bin/sh
set -eu

psql --set=ON_ERROR_STOP=1 \
  --set=api_password="$WMS_API_DB_PASSWORD" \
  --set=worker_password="$WMS_WORKER_DB_PASSWORD" \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" <<'SQL'
SELECT format(
  'CREATE ROLE wms_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',
  :'api_password'
) WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'wms_app') \gexec
ALTER ROLE wms_app PASSWORD :'api_password';
GRANT wms_api TO wms_app;

SELECT format(
  'CREATE ROLE wms_worker_login LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION BYPASSRLS PASSWORD %L',
  :'worker_password'
) WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'wms_worker_login') \gexec
ALTER ROLE wms_worker_login PASSWORD :'worker_password';
GRANT wms_worker TO wms_worker_login;
SQL
