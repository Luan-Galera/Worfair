-- Worfair — init do ambiente de DESENVOLVIMENTO (docker compose)
-- Executado uma única vez na criação do volume (postgres:16).
-- Em staging/produção, roles/permissões são gerenciadas pelo IaC (docs/devops).

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Roles de acesso (R-02/R-11 de docs/database/02)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'worfair_migrator') THEN
        CREATE ROLE worfair_migrator WITH LOGIN PASSWORD 'worfair_migrator_dev_password' BYPASSRLS;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'worfair_app') THEN
        CREATE ROLE worfair_app WITH LOGIN PASSWORD 'worfair_app_dev_password';
    END IF;
END $$;

-- Schemas dos módulos (docs/database/03)
CREATE SCHEMA IF NOT EXISTS tenancy;
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS jobs;
CREATE SCHEMA IF NOT EXISTS recruitment;
CREATE SCHEMA IF NOT EXISTS proposals;
CREATE SCHEMA IF NOT EXISTS financial;
CREATE SCHEMA IF NOT EXISTS notifications;
CREATE SCHEMA IF NOT EXISTS audit;

GRANT USAGE ON SCHEMA tenancy, identity, jobs, recruitment,
    proposals, financial, notifications, audit TO worfair_app, worfair_migrator;

-- Default privileges: tabelas futuras já nascem com grants para a app
ALTER DEFAULT PRIVILEGES IN SCHEMA tenancy, identity, jobs, recruitment,
    proposals, financial, notifications, audit
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO worfair_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA tenancy, identity, jobs, recruitment,
    proposals, financial, notifications, audit
    GRANT ALL ON TABLES TO worfair_migrator;

-- RLS fica por conta das migrations de cada módulo (políticas por tabela)