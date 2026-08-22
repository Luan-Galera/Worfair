# 02 — Proteção contra Manipulação de Tenant

## 1. Regra fundamental (SEC-02)

> **O Tenant efetivo é determinado exclusivamente pelo contexto de autenticação.
> O backend rejeita qualquer tentativa do cliente de escolher, alterar ou
> informar o `TenantId`.**

Fontes de `TenantId` **proibidas**: header `X-Tenant-Id`, body/query de
requisições de negócio, cookies, parâmetros de rota usados como contexto global.

## 2. Header `X-Tenant-Id` — rejeitado de forma explícita

```csharp
// Api/Middleware/TenantHeaderGuardMiddleware.cs
public sealed class TenantHeaderGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "X-Tenant-Id não é aceito. O tenant é derivado do token de autenticação."
            });
            return;
        }
        await next(context);
    }
}
```

> Registrar **antes** de qualquer endpoint que consulte `ITenantProvider`.

## 3. Tenant efetivo: única fonte — o token validado

```csharp
// Api/Middleware/TenantContextMiddleware.cs (atualização da versão do doc architecture/04)
public sealed class TenantContextMiddleware(ITenantProvider tenantProvider)
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // 1. Só lê o claim do token JÁ validado pelo JwtBearer (assinatura/exp/iss)
        // 2. Nunca lê headers, body ou query
        var claim = context.User.FindFirstValue(CustomClaims.TenantId);
        if (claim is not null && Guid.TryParse(claim, out var tenantId))
            tenantProvider.SetTenant(new TenantId(tenantId));

        await next(context);
    }
}
```

Regras de propagação:

- Contexto **global** (SUPER_ADMIN): claim `tenant_id` **ausente/null** →
  `ITenantProvider.TenantId = null`; só endpoints com política `Global` (SEC-03)
  são alcançáveis.
- Contexto de **tenant**: claim presente → middleware seta o provider; endpoints
  com `TenantScopeRequirement` exigem essa presença.
- A troca de contexto **só** ocorre via novo token (seção 4) — nunca por
  mutação de estado em memória.

## 4. Switch de tenant — a única via de troca

```csharp
// Identity API — único endpoint que altera o tenant do contexto
[HttpPost("api/identity/switch-tenant")]
public async Task<IActionResult> SwitchTenant(SwitchTenantRequest request, CancellationToken ct)
{
    // 1. Usuário autenticado (token atual) — qualquer tenant_id do body é IGNORADO
    //    como contexto: serve apenas como alvo de troca validado no servidor.
    var user = await users.GetActiveAsync(UserIdFrom(User), ct);
    if (user is null) return Unauthorized();

    // 2. Valida membership ATIVA + tenant ATIVO no banco
    var membership = await memberships.GetActiveAsync(user.Id, request.TargetTenantId, ct);
    if (membership is null) return NotFound();

    // 3. Roles atuais no tenant de destino + modo derivado (SEC-03)
    var roles = await access.GetRolesAsync(user.Id, request.TargetTenantId, ct);
    var mode = AccessModeMapper.Derive(roles);

    // 4. Emite NOVO access token (tenant_id = destino) e rotaciona refresh
    var token = tokenService.IssueAccessToken(user, request.TargetTenantId, roles, mode);
    var refresh = await tokenService.RotateRefreshTokenAsync(user.Id, ct);

    // 5. Audita a troca (audit.audit_logs)
    await audit.RecordAsync("identity.tenant_switch", user.Id, request.TargetTenantId, ct);

    return Ok(new AuthResponse(token, refresh));
}
```

**Resultado:** para acessar outro tenant, o usuário **sempre** obtém um novo
token emitido pelo servidor após validação dos vínculos reais. Um token válido
para o tenant A nunca acessa dados do tenant B — nem por header, nem por body,
nem por rota.

## 5. Recursos por tenant: o recurso deve "bater" com o contexto

Regra de endpoints de negócio (ex.: `/api/recruitment/requisitions/{id}`):

1. O `tenant_id` do **recurso** (lido do banco) deve ser igual ao `tenant_id`
   do contexto autenticado — verificado pelo `ManageRequisitionAuthorizationHandler`
   (doc 04).
2. Em caso de divergência (ou recurso inexistente para o tenant): **404**, nunca
   403 — não revela a existência do recurso de outro tenant.
3. RLS do banco é a **terceira linha de defesa** (R-02): mesmo com bug na
   aplicação, a query retorna vazio.

## 6. SUPER_ADMIN e contexto global

- `SUPER_ADMIN` opera em contexto **global** (`tenant_id` null) para
  administração da plataforma (`platform.*`).
- Para suporte a um tenant específico, usa o mesmo `switch-tenant` — recebe um
  token com `tenant_id` daquele tenant e age **dentro** das regras do tenant
  (RLS incluída). A troca é auditada.
- `SUPER_ADMIN` **nunca** injeta tenant via requisição; sempre via token.

## 7. Checklist anti-manipulação

- [ ] `X-Tenant-Id` rejeitado com 400 (middleware).
- [ ] `ITenantProvider` alimentado somente por claim validado.
- [ ] Handlers/Commands não aceitam `TenantId` como input (D-08/arquitetura).
- [ ] Switch de tenant só pelo endpoint controlado (membership ativa + auditoria).
- [ ] Divergência tenant recurso × contexto ⇒ 404.
- [ ] Testes de integração: token do tenant A + ID de recurso do tenant B ⇒ 404;
      header `X-Tenant-Id` ⇒ 400; sem tenant no token + endpoint tenant ⇒ 403.