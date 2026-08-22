# 02 — Validação de Tenant (Backend-Centric)

## 1. Princípio (FE-02 / SEC-02)

> **O `TenantId` enviado pelo cliente nunca é fonte confiável de autorização.
> O frontend pode informar qual contexto o usuário tenta utilizar; quem decide
> se ele pode utilizá-lo é estritamente o backend.**

Consequências práticas no frontend:

| O que o frontend FAZ | O que o frontend NÃO faz |
| -------------------- | ------------------------ |
| Envia o JWT (que carrega `tenant_id` validado) | Não envia `X-Tenant-Id` em nenhuma requisição |
| Chama `switch-tenant` com a **intenção** de trocar | Não injeta tenant em body/query de endpoints de negócio |
| Exibe o tenant ativo a partir de `/identity/me` | Não "adivinha" tenant por URL/parâmetro |
| Reage a 403/404 do backend | Não decide permissões localmente |

## 2. Origem do tenant ativo no cliente

```js
// GET /api/identity/me → calculado no servidor (permissões, modos, memberships)
{
  "user": { "id": "…", "fullName": "…" },
  "tenant": { "id": "…", "name": "ACME Ltda", "slug": "acme" },
  "memberships": [
    { "tenantId": "…", "tenantName": "ACME Ltda", "status": "Active" },
    { "tenantId": "…", "tenantName": "Beta Serviços", "status": "Active" }
  ],
  "permissions": ["recruitment.requisition.manage", "…"],
  "availableModes": ["contracting", "provider"],
  "activeMode": "contracting"
}
```

- O tenant exibido na UI é **sempre** o do payload de `/me` (que reflete o token).
- A lista de troca (`TenantSwitcher`) vem de `memberships` — o usuário só vê
  tenants aos quais **o backend confirmou vínculo ativo**.

## 3. Troca de tenant (a única forma de mudar contexto)

```js
// src/components/layout/TenantSwitcher.jsx (resumo)
export function TenantSwitcher() {
  const { user, switchTenant } = useAuth();
  const [busy, setBusy] = useState(false);

  const handleSelect = async (tenantId) => {
    setBusy(true);
    try {
      // 1. Informa a INTENÇÃO ao backend; ele valida membership ativa + tenant ativo
      // 2. Recebe NOVO par de tokens com tenant_id = destino
      // 3. Atualiza estado → router re-renderiza com o novo contexto
      await switchTenant(tenantId);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="dropdown">
      <button className="btn btn-outline-secondary dropdown-toggle" disabled={busy}>
        <i className="bi bi-buildings me-1" />{user?.tenant?.name}
      </button>
      <ul className="dropdown-menu">
        {user?.memberships.map((m) => (
          <li key={m.tenantId}>
            <button className="dropdown-item" onClick={() => handleSelect(m.tenantId)}>
              {m.tenantName}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
```

**O que o backend faz (já definido em `docs/security/02`):** ignora qualquer
tenant do request, valida membership no banco, emite novo token — ou responde
403. O frontend apenas executa a intenção.

## 4. Comportamento diante de 403/404 (sem "consertar" do lado do cliente)

| Resposta | Causa provável | Ação da UI |
| -------- | -------------- | ---------- |
| `403` | permissão efetiva ausente OU modo incompatível | `PermissionDenied` com CTA "trocar de modo" (se o `/me` indicar modo disponível) |
| `403` | tenant suspenso/desativado (válido no momento do login) | logout + mensagem "sessão do tenant expirada" |
| `404` | recurso inexistente **ou de outro tenant** (indistinguível de propósito) | tela "não encontrado" genérica — **sem** sugerir troca de tenant |
| `401` | token expirado | refresh automático; falha → tela de login |

> Regra de ouro: o frontend trata 404 de recurso como "não existe" — nunca
> pergunta "quer acessar como outro tenant?", pois isso vazaria a existência
> de dados de terceiros (SEC-02).

## 5. URL e rotas: sem tenant no path

- Rotas de aplicação **não** contêm `tenant_id` (`/app/contracting/requisitions/…`)
  — o contexto vem do token, não da URL (evita compartilhar/alterar contexto
  por link).
- Exceção única: área `SUPER_ADMIN` (`/platform/tenants/:tenantId/…`) — o
  endpoint global valida a role `SUPER_ADMIN` no servidor.

## 6. Checklist do frontend

- [ ] Nenhuma chamada Axios adiciona `X-Tenant-Id`/tenant em body.
- [ ] Troca de tenant/modo somente via `switch-tenant`/`switch-mode`.
- [ ] Tenant exibido vem de `/me` (nunca de input do usuário).
- [ ] 404 genérico; 403 com CTA condicional (baseado em `availableModes` do `/me`).
- [ ] Testes: interceptar requisições e garantir ausência de tenant em headers.