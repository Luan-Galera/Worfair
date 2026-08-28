using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worfair.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "identity",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_global = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_global = table.Column<bool>(type: "boolean", nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    email_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                schema: "identity",
                table: "outbox_messages",
                columns: new[] { "processed_on", "occurred_on" },
                filter: "processed_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_permissions_code",
                schema: "identity",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_tenant_user",
                schema: "identity",
                table: "refresh_tokens",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "uq_refresh_tokens_token_hash",
                schema: "identity",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_roles_code",
                schema: "identity",
                table: "roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_roles_id_is_global",
                schema: "identity",
                table: "roles",
                columns: new[] { "id", "is_global" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_tenant_role",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_user",
                schema: "identity",
                table: "user_roles",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_roles_user_tenant_role",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "user_id", "tenant_id", "role_id" });
                        // ─────────────────────────────────────────────────────────────────────
        // SQL normativo (docs/database/02 R-01..R-11 e docs/security/01):
        // índice funcional lower(email), CHECKs de escopo, FKs whitelisted do
        // núcleo Tenancy↔Identity (R-07), RLS nas tabelas de escopo misto,
        // seed idempotente de roles/permissões/vínculos.
        // ─────────────────────────────────────────────────────────────────────
        migrationBuilder.Sql(@"
-- R-01: unicidade global por e-mail normalizado + UNIQUE (tenant_id, id)
CREATE UNIQUE INDEX IF NOT EXISTS uq_users_email_lower ON identity.users (lower(email));
CREATE UNIQUE INDEX IF NOT EXISTS uq_refresh_tokens_tenant_id ON identity.refresh_tokens (tenant_id, id);

-- R-04/R-05: chave lógica — linhas GLOBAIS únicas por (user, role)
CREATE UNIQUE INDEX IF NOT EXISTS uq_user_roles_global
    ON identity.user_roles (user_id, role_id) WHERE tenant_id IS NULL;

-- CHECK: role global ⇔ tenant NULL  (espelho do domínio, defesa no banco)
ALTER TABLE identity.user_roles DROP CONSTRAINT IF EXISTS chk_user_roles_scope;
ALTER TABLE identity.user_roles
    ADD CONSTRAINT chk_user_roles_scope CHECK ((tenant_id IS NULL) = is_global);

-- R-07 (whitelist): FKs do núcleo Tenancy ↔ Identity
-- user_roles.role_id → roles.id
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_user_roles_role') THEN
        ALTER TABLE identity.user_roles
            ADD CONSTRAINT fk_user_roles_role
            FOREIGN KEY (role_id) REFERENCES identity.roles (id) ON DELETE CASCADE;
    END IF;
END $$;

-- user_roles.(user_id, tenant_id) → tenant_memberships (membership ativa exigida, R-05)
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_user_roles_membership') THEN
        ALTER TABLE identity.user_roles
            ADD CONSTRAINT fk_user_roles_membership
            FOREIGN KEY (user_id, tenant_id)
            REFERENCES tenancy.tenant_memberships (user_id, tenant_id);
    END IF;
END $$;

-- tenant_memberships.user_id → users.id (núcleo)
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_tenant_memberships_user') THEN
        ALTER TABLE tenancy.tenant_memberships
            ADD CONSTRAINT fk_tenant_memberships_user
            FOREIGN KEY (user_id) REFERENCES identity.users (id) ON DELETE CASCADE;
    END IF;
END $$;

-- refresh_tokens.user_id → users.id (mesmo módulo/schema)
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_refresh_tokens_user') THEN
        ALTER TABLE identity.refresh_tokens
            ADD CONSTRAINT fk_refresh_tokens_user
            FOREIGN KEY (user_id) REFERENCES identity.users (id) ON DELETE CASCADE;
    END IF;
END $$;

-- role_permissions → roles / permissions (FKs já geradas pelo EF; garantidas)

-- R-02: RLS ESCOPO MISTO — user_roles (tenant OU global, nunca misturados)
ALTER TABLE identity.user_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.user_roles FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS scope_isolation ON identity.user_roles;
CREATE POLICY scope_isolation ON identity.user_roles
    FOR ALL
    USING ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL))
    WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL));

-- RLS padrão — refresh_tokens (tenant-owned; sessões globais do SUPER_ADMIN
-- usam tenant_id NULL e são visíveis apenas em contexto global)
ALTER TABLE identity.refresh_tokens ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.refresh_tokens FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS scope_isolation ON identity.refresh_tokens;
CREATE POLICY scope_isolation ON identity.refresh_tokens
    FOR ALL
    USING ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL))
    WITH CHECK ((tenant_id::text = current_setting('app.tenant_id', true))
        OR (tenant_id IS NULL AND current_setting('app.tenant_id', true) IS NULL));

-- ─── Seed idempotente (docs/database/04 §5) ───────────────────────────────

INSERT INTO identity.roles (id, code, name, is_global, description) VALUES
    (gen_random_uuid(), 'SUPER_ADMIN',    'Super Administrador',   true,  'Acesso global à plataforma (sem tenant)'),
    (gen_random_uuid(), 'OWNER',          'Proprietário',          false, 'Dono do tenant; controle total do tenant'),
    (gen_random_uuid(), 'CLIENT',         'Cliente Contratante',   false, 'Contrata serviços; decide propostas'),
    (gen_random_uuid(), 'RECRUITER',      'Recrutador',            false, 'Gerencia requisições e pipeline'),
    (gen_random_uuid(), 'HIRING_MANAGER', 'Gestor de Contratação', false, 'Dono da requisição; decisão final e oferta'),
    (gen_random_uuid(), 'PROVIDER',       'Prestador',             false, 'Envia propostas, executa serviços, recebe pagamento')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.permissions (id, code) VALUES
    (gen_random_uuid(), 'tenants.settings.read'),
    (gen_random_uuid(), 'tenants.settings.write'),
    (gen_random_uuid(), 'tenants.members.manage'),
    (gen_random_uuid(), 'recruitment.requisition.create'),
    (gen_random_uuid(), 'recruitment.requisition.publish'),
    (gen_random_uuid(), 'recruitment.requisition.close'),
    (gen_random_uuid(), 'recruitment.requisition.team.manage'),
    (gen_random_uuid(), 'recruitment.candidate.read'),
    (gen_random_uuid(), 'recruitment.candidate.advance'),
    (gen_random_uuid(), 'recruitment.candidate.hire'),
    (gen_random_uuid(), 'recruitment.interview.schedule'),
    (gen_random_uuid(), 'recruitment.interview.feedback'),
    (gen_random_uuid(), 'jobs.posting.publish'),
    (gen_random_uuid(), 'jobs.project.create'),
    (gen_random_uuid(), 'proposals.decide'),
    (gen_random_uuid(), 'proposals.offer.issue'),
    (gen_random_uuid(), 'proposals.submit'),
    (gen_random_uuid(), 'jobs.project.apply'),
    (gen_random_uuid(), 'financial.invoice.issue'),
    (gen_random_uuid(), 'financial.payment.record'),
    (gen_random_uuid(), 'financial.payout.manage'),
    (gen_random_uuid(), 'audit.read'),
    (gen_random_uuid(), 'platform.tenants.manage'),
    (gen_random_uuid(), 'platform.users.manage')
ON CONFLICT (code) DO NOTHING;

-- OWNER e HIRING_MANAGER
INSERT INTO identity.role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'tenants.settings.read','tenants.settings.write','tenants.members.manage',
    'recruitment.requisition.create','recruitment.requisition.publish',
    'recruitment.requisition.close','recruitment.requisition.team.manage',
    'recruitment.candidate.read','recruitment.candidate.advance',
    'recruitment.candidate.hire','recruitment.interview.schedule',
    'recruitment.interview.feedback','jobs.posting.publish','jobs.project.create',
    'proposals.decide','proposals.offer.issue',
    'financial.invoice.issue','financial.payment.record','audit.read')
WHERE r.code IN ('OWNER','HIRING_MANAGER')
ON CONFLICT DO NOTHING;

-- RECRUITER
INSERT INTO identity.role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'recruitment.requisition.create','recruitment.requisition.publish',
    'recruitment.requisition.close','recruitment.requisition.team.manage',
    'recruitment.candidate.read','recruitment.candidate.advance',
    'recruitment.interview.schedule','recruitment.interview.feedback',
    'jobs.posting.publish')
WHERE r.code = 'RECRUITER'
ON CONFLICT DO NOTHING;

-- CLIENT
INSERT INTO identity.role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'recruitment.candidate.read','jobs.project.create','proposals.decide',
    'proposals.offer.issue','financial.invoice.issue','audit.read')
WHERE r.code = 'CLIENT'
ON CONFLICT DO NOTHING;

-- PROVIDER
INSERT INTO identity.role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'proposals.submit','jobs.project.apply','financial.payout.manage')
WHERE r.code = 'PROVIDER'
ON CONFLICT DO NOTHING;

-- SUPER_ADMIN (permissões platform.*)
INSERT INTO identity.role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'platform.tenants.manage','platform.users.manage')
WHERE r.code = 'SUPER_ADMIN' AND r.is_global
ON CONFLICT DO NOTHING;
");
}

        /// <inheritdoc />

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
