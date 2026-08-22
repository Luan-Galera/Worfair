# 04 — Scripts SQL Iniciais e Mapeamento EF Core

DDL de referência para as tabelas críticas (`tenants`, `companies`, `users`,
`roles`, `user_roles`) + RLS + seed + mapeamento EF Core. Executar na ordem
apresentada. PostgreSQL 16+.

## 0. Roles de banco e schemas

```sql
-- Roles de acesso (R-02/R-11)
CREATE ROLE worfair_migrator WITH LOGIN BYPASSRLS;   -- executa migrações e seed
CREATE ROLE worfair_app     WITH LOGIN;              -- runtime da aplicação (sujeito a RLS)

-- Schemas dos módulos
CREATE SCHEMA IF NOT EXISTS tenancy;
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS jobs;
CREATE SCHEMA IF NOT EXISTS recruitment;
CREATE SCHEMA IF NOT EXISTS proposals;
CREATE SCHEMA IF NOT EXISTS financial;
CREATE SCHEMA IF NOT EXISTS notifications;
CREATE SCHEMA IF NOT EXISTS audit;
```

## 1. Tabelas críticas — `tenancy`

```sql
-- 1.1 tenants (GLOBAL — sem RLS, sem tenant_id)
CREATE TABLE tenancy.tenants (
    id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name       varchar(150) NOT NULL,
    slug       varchar(80)  NOT NULL UNIQUE,
    tier       smallint     NOT NULL DEFAULT 1 CHECK (tier IN (1, 2, 3)),      -- 1=Standard 2=Pro 3=Enterprise
    status     smallint     NOT NULL DEFAULT 1 CHECK (status IN (1, 2, 3)),    -- 1=Active 2=Suspended 3=Cancelled
    timezone   varchar(50),
    locale     varchar(10)  NOT NULL DEFAULT 'pt-BR',
    created_at timestamptz  NOT NULL DEFAULT now(),
    updated_at timestamptz  NOT NULL DEFAULT now()
);

-- 1.2 companies (TENANT-OWNED)
CREATE TABLE tenancy.companies (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid NOT NULL REFERENCES tenancy.tenants (id) ON DELETE RESTRICT,
    legal_name  varchar(200) NOT NULL,
    trade_name  varchar(200),
    document    varchar(20)  NOT NULL,
    email       varchar(320),
    phone       varchar(30),
    status      smallint NOT NULL DEFAULT 1 CHECK (status IN (1, 2)),
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_companies_tenant_document UNIQUE (tenant_id, document)
);
CREATE INDEX ix_companies_tenant_id ON tenancy.companies (tenant_id);

-- 1.3 tenant_memberships (TENANT-OWNED; núcleo Tenancy ↔ Identity)
CREATE TABLE tenancy.tenant_memberships (
    tenant_id uuid NOT NULL REFERENCES tenancy.tenants (id) ON DELETE CASCADE,
    user_id   uuid NOT NULL REFERENCES identity.users  (id) ON DELETE CASCADE,
    status    smallint NOT NULL DEFAULT 1 CHECK (status IN (1, 2, 3)),          -- 1=Active 2=Invited 3=Disabled
    joined_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, user_id)
);
CREATE INDEX ix_tenant_memberships_user ON tenancy.tenant_memberships (user_id);
```

## 2. Tabelas críticas — `identity`

```sql
-- 2.1 users (GLOBAL — sem RLS)
CREATE TABLE identity.users (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email            varchar(320) NOT NULL,
    password_hash    text         NOT NULL,
    full_name        varchar(200) NOT NULL,
    status           smallint     NOT NULL DEFAULT 1 CHECK (status IN (1, 2, 3)), -- 1=Active 2=Locked 3=Disabled
    email_verified_at timestamptz,
    last_login_at    timestamptz,
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX uq_users_email_lower ON identity.users (lower(email));
CREATE INDEX ix_users_status ON identity.users (status);

-- 2.2 roles (GLOBAL) — catálogo de roles (DB-02/DB-03)
CREATE TABLE identity.roles (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code        varchar(50)  NOT NULL UNIQUE,
    name        varchar(120) NOT NULL,
    is_global   boolean      NOT NULL DEFAULT false,   -- true apenas para SUPER_ADMIN
    description text
);
-- Alvo da FK composta (R-04): garante consistência role × escopo
CREATE UNIQUE INDEX uq_roles_id_is_global ON identity.roles (id, is_global);

-- 2.3 permissions (GLOBAL)
CREATE TABLE identity.permissions (
    id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(100) NOT NULL UNIQUE
);

-- 2.4 role_permissions (GLOBAL)
CREATE TABLE identity.role_permissions (
    role_id       uuid NOT NULL REFERENCES identity.roles (id)       ON DELETE CASCADE,
    permission_id uuid NOT NULL REFERENCES identity.permissions (id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

-- 2.5 user_roles (escopo misto: tenant → role de tenant; NULL → role global)
CREATE TABLE identity.user_roles (
    user_id    uuid        NOT NULL REFERENCES identity.users (id) ON DELETE CASCADE,
    tenant_id  uuid        NULL,          -- NULL = role global (SUPER_ADMIN)
    role_id    uuid        NOT NULL,
    is_global  boolean     NOT NULL,      -- espelho da role para a FK composta
    granted_by uuid        NULL,
    granted_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, tenant_id, role_id),

    -- R-04: role referenciada deve ter o mesmo is_global da linha
    CONSTRAINT fk_user_roles_role_scope
        FOREIGN KEY (role_id, is_global) REFERENCES identity.roles (id, is_global),

    -- R-04: role global exige tenant NULL; role de tenant exige tenant presente
    CONSTRAINT chk_user_roles_scope CHECK ((tenant_id IS NULL) = is_global),

    -- R-05: linhas com tenant exigem membership ativa (MATCH SIMPLE ignora NULL)
    CONSTRAINT fk_user_roles_membership
        FOREIGN KEY (user_id, tenant_id)
        REFERENCES tenancy.tenant_memberships (user_id, tenant_id)
);
CREATE INDEX ix_user_roles_tenant_role ON identity.user_roles (tenant_id, role_id);
CREATE INDEX ix_user_roles_user        ON identity.user_roles (user_id);
```

> **Exemplos de linhas válidas:**
> `(user A, tenant T1, OWNER,  false)` · `(user A, tenant T1, RECRUITER, false)` ·
> `(user A, tenant T1, HIRING_MANAGER, false)` → **3 roles simultâneas no mesmo tenant**.
> `(user S, NULL, SUPER_ADMIN, true)` → **global, sem tenant**.
> Linha `(user S, NULL, RECRUITER, false)` é **rejeitada pelo CHECK**.

## 3. RLS nas tabelas tenant-owned (R-02)

```sql
-- Helper: define o tenant da sessão (usado pela app via interceptor EF e por
-- administradores em sessões psql)
CREATE OR REPLACE FUNCTION tenancy.set_tenant(p_tenant_id uuid)
RETURNS void LANGUAGE sql AS $$
    SELECT set_config('app.tenant_id', p_tenant_id::text, false);
$$;

-- --- tenancy.companies ---
ALTER TABLE tenancy.companies ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.companies FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON tenancy.companies
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));

-- --- tenancy.tenant_memberships ---
ALTER TABLE tenancy.tenant_memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.tenant_memberships FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON tenancy.tenant_memberships
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));

-- --- identity.user_roles (escopo misto: tenant OU global, nunca misturado) ---
ALTER TABLE identity.user_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.user_roles FORCE ROW LEVEL SECURITY;
CREATE POLICY scope_isolation ON identity.user_roles
    FOR ALL
    USING ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL))
    WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL));

-- --- Exemplo de extensão aos demais módulos (uma política por tabela) ---
CREATE TABLE recruitment.job_requisitions (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid NOT NULL REFERENCES tenancy.tenants (id) ON DELETE RESTRICT,
    company_id       uuid,
    title            varchar(120) NOT NULL,
    description      text NOT NULL,
    salary_min       numeric(12,2),
    salary_max       numeric(12,2) CHECK (salary_max IS NULL OR salary_min <= salary_max),
    salary_currency  char(3) NOT NULL,
    status           smallint NOT NULL DEFAULT 1 CHECK (status BETWEEN 1 AND 5),
    published_at     timestamptz,
    closed_at        timestamptz,
    close_reason     text,
    created_by       uuid,
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX uq_job_requisitions_tenant_id ON recruitment.job_requisitions (tenant_id, id);
CREATE INDEX ix_job_requisitions_tenant_status    ON recruitment.job_requisitions (tenant_id, status);
CREATE INDEX ix_job_requisitions_tenant_company   ON recruitment.job_requisitions (tenant_id, company_id);

ALTER TABLE recruitment.job_requisitions ENABLE ROW LEVEL SECURITY;
ALTER TABLE recruitment.job_requisitions FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON recruitment.job_requisitions
    FOR ALL
    USING (tenant_id::text = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));
```

## 4. Permissões de acesso (R-11)

```sql
GRANT USAGE ON SCHEMA tenancy, identity, recruitment TO worfair_app;

GRANT SELECT, INSERT, UPDATE, DELETE
    ON ALL TABLES IN SCHEMA tenancy, identity, recruitment TO worfair_app;

-- Aplicar também em tabelas criadas no futuro:
ALTER DEFAULT PRIVILEGES FOR ROLE worfair_migrator IN SCHEMA tenancy, identity, recruitment
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO worfair_app;
```

## 5. Seed de roles e permissões (idempotente)

```sql
-- 5.1 Roles (catálogo oficial v1.2 — DB-07)
INSERT INTO identity.roles (code, name, is_global, description) VALUES
    ('SUPER_ADMIN',    'Super Administrador',   true,  'Acesso global à plataforma (sem tenant)'),
    ('OWNER',          'Proprietário',          false, 'Dono do tenant; controle total do tenant'),
    ('CLIENT',         'Cliente Contratante',   false, 'Contrata serviços; decide propostas'),
    ('RECRUITER',      'Recrutador',            false, 'Gerencia requisições e pipeline'),
    ('HIRING_MANAGER', 'Gestor de Contratação', false, 'Dono da requisição; decisão final e oferta'),
    ('PROVIDER',       'Prestador',             false, 'Envia propostas, executa serviços, recebe pagamento')
ON CONFLICT (code) DO NOTHING;

-- 5.2 Permissões (amostra; granularidade por permissão — SOURCER/INTERVIEWER/FINANCE
--     foram absorvidos como permissões das roles oficiais)
INSERT INTO identity.permissions (code) VALUES
    -- Tenants
    ('tenants.settings.read'), ('tenants.settings.write'), ('tenants.members.manage'),
    -- Recruitment (Contratante: RECRUITER / HIRING_MANAGER / OWNER / CLIENT)
    ('recruitment.requisition.create'), ('recruitment.requisition.publish'),
    ('recruitment.requisition.close'), ('recruitment.requisition.team.manage'),
    ('recruitment.candidate.read'), ('recruitment.candidate.advance'),
    ('recruitment.candidate.hire'), ('recruitment.interview.schedule'),
    ('recruitment.interview.feedback'),
    -- Jobs (Contratante)
    ('jobs.posting.publish'), ('jobs.project.create'),
    -- Proposals (Contratante)
    ('proposals.decide'), ('proposals.offer.issue'),
    -- Proposals (Prestador)
    ('proposals.submit'), ('jobs.project.apply'),
    -- Financial (Contratante)
    ('financial.invoice.issue'), ('financial.payment.record'),
    -- Financial (Prestador)
    ('financial.payout.manage'),
    -- Auditoria (Contratante)
    ('audit.read'),
    -- Global (plataforma — SUPER_ADMIN)
    ('platform.tenants.manage'), ('platform.users.manage')
ON CONFLICT (code) DO NOTHING;

-- 5.3 Vínculo role → permissões (amostra; permissões efetivas = união das roles)
INSERT INTO identity.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'tenants.settings.read', 'tenants.settings.write', 'tenants.members.manage',
    'recruitment.requisition.create', 'recruitment.requisition.publish',
    'recruitment.requisition.close', 'recruitment.requisition.team.manage',
    'recruitment.candidate.read', 'recruitment.candidate.advance',
    'recruitment.candidate.hire', 'recruitment.interview.schedule',
    'recruitment.interview.feedback', 'jobs.posting.publish', 'jobs.project.create',
    'proposals.decide', 'proposals.offer.issue',
    'financial.invoice.issue', 'financial.payment.record', 'audit.read'
)
WHERE r.code IN ('OWNER', 'HIRING_MANAGER')
ON CONFLICT DO NOTHING;

INSERT INTO identity.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'recruitment.requisition.create', 'recruitment.requisition.publish',
    'recruitment.requisition.close', 'recruitment.requisition.team.manage',
    'recruitment.candidate.read', 'recruitment.candidate.advance',
    'recruitment.interview.schedule', 'recruitment.interview.feedback',
    'jobs.posting.publish'
)
WHERE r.code = 'RECRUITER'
ON CONFLICT DO NOTHING;

INSERT INTO identity.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'recruitment.candidate.read', 'jobs.project.create', 'proposals.decide',
    'proposals.offer.issue', 'financial.invoice.issue', 'audit.read'
)
WHERE r.code = 'CLIENT'
ON CONFLICT DO NOTHING;

INSERT INTO identity.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'proposals.submit', 'jobs.project.apply', 'financial.payout.manage'
)
WHERE r.code = 'PROVIDER'
ON CONFLICT DO NOTHING;
```

## 6. Prova de isolamento (validação manual)

```sql
-- Contexto: usuário do tenant A
SELECT tenancy.set_tenant('aaaaaaaa-0000-0000-0000-000000000000');
SELECT * FROM recruitment.job_requisitions;   -- apenas linhas do tenant A

-- Contexto: usuário do tenant B (mesmo SQL, sem alteração de query)
SELECT tenancy.set_tenant('bbbbbbbb-0000-0000-0000-000000000000');
SELECT * FROM recruitment.job_requisitions;   -- apenas linhas do tenant B

-- Sem contexto (erro de programação / sessão esquecida)
SELECT set_config('app.tenant_id', '', false);
SELECT * FROM recruitment.job_requisitions;   -- 0 linhas (deny-by-default, R-02)

-- Cross-tenant forçado é bloqueado pelo WITH CHECK:
-- INSERT ... VALUES (tenant A) em sessão do tenant B → viola policy
```

## 7. Mapeamento conceitual EF Core (tabelas críticas)

As entidades vivem nos módulos `Tenants` (Tenant, Company, TenantMembership) e
`Identity` (User, Role, Permission, RolePermission, UserRole). Resumo do mapeamento:

```csharp
// ---- Módulo Tenants: Domain/Tenant.cs (global) ----
public sealed class Tenant : Entity<TenantId>      // NÃO implementa ITenantEntity
{
    public TenantId Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public TenantTier Tier { get; private set; }
    public TenantStatus Status { get; private set; }
    public string? Timezone { get; private set; }
}

// ---- Módulo Tenants: Domain/Company.cs (tenant-owned) ----
public sealed class Company : TenantEntity        // TenantEntity : Entity, ITenantEntity
{
    public CompanyId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string LegalName { get; private set; } = default!;
    public string Document { get; private set; } = default!;
    public CompanyStatus Status { get; private set; }
}

// ---- Módulo Identity: Domain/UserRole.cs (escopo misto) ----
public sealed class UserRole
{
    public UserId UserId { get; private set; }
    public TenantId? TenantId { get; private set; }     // NULL = SUPER_ADMIN global
    public RoleId RoleId { get; private set; }
    public DateTime GrantedAtUtc { get; private set; }
}

// ---- Persistence/Configurations/UserRoleConfiguration.cs ----
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles", "identity");

        builder.HasKey(ur => new { ur.UserId, ur.TenantId, ur.RoleId });

        builder.Property(ur => ur.TenantId)
            .HasConversion(id => id!.Value.Value, v => new TenantId(v))
            .IsRequired(false);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(ur => ur.RoleId);
    }
}

// ---- Persistence/Configurations/TenantConfiguration.cs ----
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", "tenancy");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Slug).HasMaxLength(80);
        builder.HasIndex(t => t.Slug).IsUnique();
    }
}
```

**Observações do mapeamento:**

1. A restrição de escopo (`chk_user_roles_scope`) e as FKs compostas
   (`fk_user_roles_role_scope`, `fk_user_roles_membership`) são **aplicadas via
   SQL de migração customizada** (não são expressáveis com fluência EF);
   registrar no MigrationBuilder: `migrationBuilder.Sql("...")`.
2. `TenantId` **não** é exposto em `CREATE`/`UPDATE` pela aplicação: é preenchido
   pelo `TenantSaveChangesInterceptor` (docs architecture/04) e protegido por
   RLS `WITH CHECK` no banco.
3. `users`, `roles`, `permissions`, `role_permissions`, `tenants`: **sem**
   `ITenantEntity` (globais).
4. `UserRole` e `TenantMembership` são **join tables** do núcleo
   Tenancy/Identity — as únicas tabelas com FKs cruzadas permitidas (R-07).