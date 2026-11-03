-- Creates the database roles. Run once per server, as a superuser, before the first migration:
--
--   psql -h <host> -U postgres -v app_password="$APP_PASSWORD" -f scripts/db/create-roles.sql
--
-- comtest_app is what the application connects with. It can only read and write data: it
-- cannot change the schema, and NOBYPASSRLS keeps it subject to row-level security.

SELECT format('CREATE ROLE comtest_app LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS NOINHERIT', :'app_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'comtest_app')
\gexec
