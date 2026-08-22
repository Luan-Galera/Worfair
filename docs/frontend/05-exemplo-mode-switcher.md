# 05 — Exemplo: Alternância de Modos (Contratante ↔ Prestador)

Componente de referência: um seletor de **Modo Operacional** em React +
Bootstrap, integrado ao gerenciamento de estado de permissões — e que **não
concede nada**: apenas informa a intenção ao backend (`switch-mode`), que
decide e reemite o token (SEC-03/FE-03).

## 1. Estado de acesso (AccessContext)

```jsx
// src/access/AccessContext.jsx
import { createContext, useContext, useEffect, useState, useCallback } from 'react';
import { authApi } from '../api/endpoints/auth';
import { useAuth } from '../auth/useAuth';

const AccessContext = createContext(null);

export const MODES = {
  contracting: { label: 'Contratante', icon: 'bi-briefcase', color: 'primary' },
  provider:    { label: 'Prestador',   icon: 'bi-person-workspace', color: 'secondary' }
};

export function AccessProvider({ children }) {
  const { user, setTokens } = useAuth();            // tokens vêm do AuthContext
  const [permissions, setPermissions] = useState([]);
  const [availableModes, setAvailableModes] = useState([]);
  const [activeMode, setActiveMode] = useState(null);
  const [loading, setLoading] = useState(true);

  // Espelho das permissões/modos calculados pelo BACKEND (GET /identity/me)
  useEffect(() => {
    if (!user?.accessToken) { setLoading(false); return; }

    setLoading(true);
    authApi.me()
      .then(({ data }) => {
        setPermissions(data.permissions ?? []);
        setAvailableModes(data.availableModes ?? []);
        setActiveMode(data.activeMode ?? null);
      })
      .catch(() => { /* 401/403 tratados pelos interceptors */ })
      .finally(() => setLoading(false));
  }, [user?.accessToken]);

  const switchMode = useCallback(async (mode) => {
    // INTENÇÃO apenas: o backend valida a permissão efetiva, reemite o token
    // com o novo `mode` e devolve o contexto atualizado. Se o usuário não
    // puder usar o modo, o servidor responde 403 — nada muda no cliente.
    const { data } = await authApi.switchMode(mode);

    setTokens(data.accessToken, data.refreshToken);   // novo token (novo mode)
    setActiveMode(data.activeMode);                   // confirmado pelo backend
    setPermissions(data.permissions ?? []);
    setAvailableModes(data.availableModes ?? []);
    return data;
  }, [setTokens]);

  const can = useCallback((permission) => permissions.includes(permission), [permissions]);

  const value = { loading, permissions, availableModes, activeMode, switchMode, can };
  return <AccessContext.Provider value={value}>{children}</AccessContext.Provider>;
}

export function useAccess() {
  const ctx = useContext(AccessContext);
  if (!ctx) throw new Error('useAccess deve ser usado dentro de <AccessProvider>');
  return ctx;
}
```

## 2. Componente `ModeSwitcher` (Bootstrap)

```jsx
// src/components/layout/ModeSwitcher.jsx
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAccess, MODES } from '../../access/useAccess';
import { useAuth } from '../../auth/useAuth';

export function ModeSwitcher() {
  const { availableModes, activeMode, switchMode } = useAccess();
  const { user } = useAuth();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  if (availableModes.length < 2) return null;   // sem alternância: não renderiza nada

  const handleSwitch = async (mode) => {
    if (mode === activeMode || busy) return;

    setBusy(true);
    setError(null);
    try {
      await switchMode(mode);                     // backend decide (403 se negado)
      navigate(`/app/${mode}/home`);              // rota do novo modo (guard reavalia)
    } catch (err) {
      // 403 ⇒ permissão efetiva não cobre o modo: o backend negou; UI só informa
      setError(err?.response?.status === 403
        ? 'O modo solicitado não está disponível para o seu perfil.'
        : 'Não foi possível alternar o modo. Tente novamente.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="d-flex align-items-center gap-2">
      {/* Segmentado Bootstrap — sem JS extra, acessível por aria-pressed */}
      <div className="btn-group" role="group" aria-label="Modo de operação">
        {availableModes.map((mode) => {
          const meta = MODES[mode];
          const active = mode === activeMode;
          return (
            <button
              key={mode}
              type="button"
              className={`btn btn-sm ${active ? `btn-${meta.color}` : 'btn-outline-secondary'}`}
              aria-pressed={active}
              disabled={busy}
              title={`Usar como ${meta.label}`}
              onClick={() => handleSwitch(mode)}
            >
              <i className={`bi ${meta.icon} me-1`} aria-hidden="true" />
              {meta.label}
            </button>
          );
        })}
      </div>

      {/* Identidade da sessão — vem do token/`/me`, nunca de input do usuário */}
      <span className="text-muted small d-none d-md-inline">
        {user?.fullName} · {user?.tenant?.name}
      </span>

      {error && (
        <div className="alert alert-warning alert-sm p-2 mb-0 small" role="alert">
          {error}
        </div>
      )}
    </div>
  );
}
```

## 3. Onde o componente entra (TopNav)

```jsx
// src/components/layout/TopNav.jsx (resumo)
export function TopNav() {
  return (
    <nav className="navbar navbar-expand navbar-light bg-white border-bottom px-3">
      <a className="navbar-brand fw-bold" href="/">Worfair</a>
      <div className="ms-auto d-flex align-items-center gap-3">
        <ModeSwitcher />
        <TenantSwitcher />
        <UserMenu />
      </div>
    </nav>
  );
}
```

## 4. Guard no nível do elemento (RequirePermission)

```jsx
// src/routing/RequirePermission.jsx
import { useAccess } from '../access/useAccess';
import { PermissionDenied } from '../components/feedback/PermissionDenied';

export function RequirePermission({ permission, mode, children }) {
  const { can, activeMode } = useAccess();

  const allowed = can(permission) && (!mode || activeMode === mode);
  if (!allowed) return <PermissionDenied reason="permissão insuficiente" />;

  return children;
}
```

Uso em rota protegida (reforça o guard composto do `ProtectedRoute`):

```jsx
<Route path="/app/contracting/requisitions/new"
       element={
         <RequirePermission permission="recruitment.requisition.manage" mode="contracting">
           <RequisitionCreatePage />
         </RequirePermission>
       } />
```

## 5. Fluxo completo da alternância (seguro por construção)

```
1. Usuário (CLIENT + PROVIDER) está em Modo Contratante (token mode=contracting)
2. Clica "Prestador" no ModeSwitcher
3. POST /identity/switch-mode { mode: "provider" }
4. Backend: revalida permissões efetivas → modo disponível? → NOVO token (mode=provider)
   - Negado ⇒ 403 ⇒ alerta amarelo; token e UI inalterados
5. Frontend: setTokens(novo) → activeMode = "provider" → navigate(/app/provider/home)
6. ProtectedRoute reavalia: modo ok → ProviderLayout renderiza (sidebar provider)
7. Toda chamada de API segue com o novo token; o backend valida permissões/modo
   em CADA request — alternar a UI nunca concedeu nada (SEC-03/FE-03)
```

## 6. Testes do componente

```jsx
// src/components/layout/ModeSwitcher.test.jsx (vitest + testing-library)
it('chama switch-mode e navega para a home do novo modo', async () => {
  // arranjo: /me devolve availableModes=["contracting","provider"], activeMode="contracting"
  render(<TestApp />);

  fireEvent.click(screen.getByRole('button', { name: /Prestador/i }));

  expect(authApi.switchMode).toHaveBeenCalledWith('provider');
  await waitFor(() => expect(screen.getByText(/Saldo e saques/)).toBeInTheDocument());
});

it('mantém estado atual quando o backend nega (403)', async () => {
  authApi.switchMode.mockRejectedValue({ response: { status: 403 } });

  fireEvent.click(screen.getByRole('button', { name: /Prestador/i }));

  expect(await screen.findByRole('alert')).toHaveTextContent('não está disponível');
  expect(screen.getByRole('button', { name: /Contratante/ })).toHaveAttribute('aria-pressed', 'true');
});

it('não renderiza quando há apenas um modo disponível', () => {
  // /me → availableModes=["contracting"]
  expect(screen.queryByRole('group', { name: /Modo de operação/ })).not.toBeInTheDocument();
});
```