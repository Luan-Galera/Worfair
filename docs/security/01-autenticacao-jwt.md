# 01 — Autenticação JWT

## 1. Princípio fundamental (SEC-01)

> **O JWT é transporte de contexto, NÃO fonte de verdade.**

O token existe para identificar *quem* é o usuário e *em qual contexto* ele
opera. **Toda decisão de autorização é tomada no servidor**, relendo o estado
real do usuário (status, membership, roles, permissões efetivas) a partir do
banco — nunca dos claims. O token nunca carrega permissões.

```
┌──────────┐  POST /api/identity/login   ┌────────────────┐
│  Cliente │ ──────────────────────────► │  Identity API  │
└──────────┘                             └────────────────┘
        ▲                                       │ valida credenciais + membership
        │  access_token (15min)                 ▼
        │  refresh_token (rotativo)      emite token com claims de CONTEXTO
        │
┌──────────┐  Bearer <access_token>      ┌─────────────────────────────────┐
│  Cliente │ ──────────────────────────► │ JwtBearer (assinatura/exp/iss)  │
└──────────┘                             └─────────────────────────────────┘
                                                 │
                                                 ▼
                              ┌──────────────────────────────────────────────┐
                              │ Autorização (a cada request):                 │
                              │  1. usuário ativo no banco                    │
                              │  2. tenant ativo + membership ativa           │
                              │  3. roles atuais do banco                     │
                              │  4. permissões efetivas = união das roles     │
                              │  5. modo compatível com permissões            │
                              │  6. recurso pertence ao tenant do contexto    │
                              └──────────────────────────────────────────────┘
```

## 2. Emissão (módulo Identity)

No login e no switch de tenant, o servidor:

1. Valida credenciais (argon2id/bcrypt via `PasswordHasher`).
2. Valida `users.status = Active`.
3. Resolve o tenant: tenant do login ou tenant alvo do switch — **exige
   membership ativa** (`tenant_memberships.status = Active`) e
   `tenants.status = Active`.
4. Lê as roles atuais do banco (`user_roles`) e deriva `mode` (tabela SEC-03).
5. Emite access token (curto) + refresh token rotativo.

### 2.1 Claims do access token

| Claim | Tipo | Valor | Observação |
| ----- | ---- | ----- | ---------- |
| `sub` | `uuid` | `user_id` | identidade do usuário |
| `jti` | `uuid` | id do token | revogação/deny-list quando necessário |
| `iss` / `aud` | `string` | emissor/audiência | fixos por ambiente |
| `iat` / `exp` | `long` | emissão/expiração | **15 min** (access) |
| `tenant_id` | `uuid?` | tenant efetivo | **`null` apenas para contexto global (SUPER_ADMIN)** |
| `roles` | `string[]` | códigos das roles no tenant | informação de contexto, NUNCA decisiva |
| `mode` | `string` | `contracting` \| `provider` \| `global` | derivado das roles na emissão, revalidado |
| `auth_time` | `long` | timestamp do login | revogação de tokens prévios a mudança de senha |

**NUNCA colocar no token:** permissões, senha/hash, dados bancários, e-mail
completo de terceiros, ou qualquer dado de negócio.

## 3. Validação no servidor (JwtBearer)

```csharp
// Api/Authentication/JwtConfiguration.cs (registro)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = jwksResolver.GetKeys(),      // RS256 público, rotação por kid
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = CustomClaims.Roles
        };
    });
```

- **RS256** (chave assimétrica): Identity guarda a privada; o Api valida com a
  pública via JWKS (`kid` na rotação). HS256 (secreto compartilhado) só em dev.
- Tokens com claims inesperados ou malformados são rejeitados na validação;
  ausência de claims obrigatórios falha na autorização (seção 4).

## 4. Revalidação obrigatória no servidor (a cada requisição)

Os handlers de autorização (doc 03/04) executam, **sempre**, a seguinte cadeia
— o `sub` e o `tenant_id` do token são apenas *chaves de busca*:

| Passo | Verificação | Origem | Se falhar |
| ----- | ----------- | ------ | --------- |
| 1 | `users.status = Active` | banco (`identity.users`) | 401/403 |
| 2 | `tenant_id` presente ⇒ `tenants.status = Active` | banco | 403 |
| 3 | `tenant_id` presente ⇒ `tenant_memberships.status = Active` | banco | 403 |
| 4 | Roles **atuais** (banco) — claims de `roles` ignorados | banco (`user_roles`) | — |
| 5 | Permissões efetivas = união de permissões das roles (banco) | banco (`role_permissions`) | 403 |
| 6 | `mode` do token ∈ modos derivados das permissões atuais | derivado | 403 |
| 7 | Recurso: `recurso.tenant_id == tenant_id` efetivo | banco (recurso) | 404 |

> **Por que não confiar nos claims:** role revogada, membership desativada,
> tenant suspenso, usuário bloqueado — tudo precisa valer **imediatamente**, não
> quando o token expirar.

### 4.1 Cache de permissões (opcional e seguro)

Para reduzir carga, o resultado dos passos 1–5 pode ser cacheado por
`(user_id, tenant_id)` com **TTL máximo de 5 minutos** e **invalidação explícita**
por integration event (`RoleChanged`, `MembershipChanged`, `UserDisabled`,
`TenantSuspended`) consumido pelo módulo Identity e propagado via Outbox.

**Regra:** recursos sensíveis (financeiro, contrato, decisão de contratação,
auditoria) **nunca** usam cache — revalidam no banco a cada acesso.

## 5. Refresh tokens (rotação + detecção de reuso)

Modelo já definido (`identity.refresh_tokens`):

| Regra | Comportamento |
| ----- | ------------- |
| Rotação | Todo refresh emite novo access + novo refresh; o anterior é revogado |
| Reuso | `token_hash` de token já revogado ⇒ **revoga toda a família** (força novo login) |
| Expiração | 7 dias, uso único |
| Revogação | `revoked_at` em logout, troca de senha, bloqueio de conta |
| Storage | somente **hash** (`token_hash`), nunca o token em claro |

## 6. Checklist de segurança do token

- [ ] `exp` curto (15 min) — impacto de roubo limitado.
- [ ] Claims de `roles`/`mode` são **informativos**: autorização usa o banco.
- [ ] Permissões nunca no token.
- [ ] `tenant_id` null apenas em contexto global (SUPER_ADMIN).
- [ ] JWKS com rotação de chaves (`kid`); rejeitar algoritmo ≠ RS256.
- [ ] Refresh token só aceita uma vez (reuso ⇒ revoga família).
- [ ] Logout revoga refresh; mudança de senha revoga todos os tokens do usuário.