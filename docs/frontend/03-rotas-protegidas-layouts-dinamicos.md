# 03 — Rotas Protegidas e Layouts Dinâmicos

## 1. Mapa de rotas × modo × permissão

Roteamento declarativo em `routeConfig.js` — uma tabela, um lugar para auditar:

```js
// src/routing/routeConfig.js
export const ROUTES = [
  // ---- Público ----
  { path: '/login', mode: 'public', layout: 'public', element: <LoginPage /> },

  // ---- Modo Contratante ----
  { path: '/app/contracting/home', mode: 'contracting', layout: 'contracting',
    permission: null, element: <ContractingHomePage /> },
  { path: '/app/contracting/requisitions', mode: 'contracting', layout: 'contracting',
    permission: 'recruitment.requisition.manage', element: <RequisitionListPage /> },
  { path: '/app/contracting/requisitions/new', mode: 'contracting', layout: 'contracting',
    permission: 'recruitment.requisition.manage', element: <RequisitionCreatePage /> },
  { path: '/app/contracting/proposals/inbox', mode: 'contracting', layout: 'contracting',
    permission: 'proposals.decide', element: <ProposalsInboxPage /> },
  { path: '/app/contracting/invoices', mode: 'contracting', layout: 'contracting',
    permission: 'financial.invoice.issue', element: <InvoicesPage /> },

  // ---- Modo Prestador ----
  { path: '/app/provider/home', mode: 'provider', layout: 'provider',
    permission: null, element: <ProviderHomePage /> },
  { path: '/app/provider/projects', mode: 'provider', layout: 'provider',
    permission: 'jobs.project.apply', element: <ProjectsPage /> },
  { path: '/app/provider/proposals/new', mode: 'provider', layout: 'provider',
    permission: 'proposals.submit', element: <ProposalCreatePage /> },
  { path: '/app/provider/balance', mode: 'provider', layout: 'provider',
    permission: 'financial.payout.manage', element: <BalancePage /> },

  // ---- Plataforma (SUPER_ADMIN, contexto global) ----
  { path: '/platform/tenants', mode: 'global', layout: 'platform',
    permission: 'platform.tenants.manage', element: <TenantsPage /> },

  // ---- Fallbacks ----
  { path: '/forbidden', mode: 'public', layout: 'public', element: <PermissionDenied /> },
  { path: '*', mode: 'public', layout: 'public', element: <NotFoundPage /> }
];
```

## 2. Guard composto (auth → modo → permissão)

```jsx
// src/routing/ProtectedRoute.jsx
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { useAccess } from '../access/useAccess';
import { LAYOUTS } from '../layouts';
import { PermissionDenied } from '../components/feedback/PermissionDenied';

export function ProtectedRoute({ mode, permission }) {
  const { user, loading: authLoading } = useAuth();
  const { activeMode, can, loading: accessLoading } = useAccess();

  if (authLoading || accessLoading) return <SkeletonShell />;

  // 1. Autenticado?
  if (!user) return <Navigate to="/login" state={{ from: useLocation() }} replace />;

  // 2. Modo da rota bate com o modo ATIVO (validado pelo backend no switch-mode)?
  if (mode === 'contracting' || mode === 'provider') {
    if (activeMode !== mode) {
      // Modo diferente do ativo: só redireciona se o modo-alvo estiver DISPONÍVEL;
      // se não estiver, mostra 403 (a decisão continua sendo do backend).
      const isAvailable = (user.availableModes ?? []).includes(mode);
      if (!isAvailable) return <PermissionDenied reason="modo indisponível" />;
      return <Navigate to={`/app/${mode}/home`} replace />;
    }
  }

  // 3. Permissão efetiva (espelho do /me — o backend ainda valida tudo)
  if (permission && !can(permission)) return <PermissionDenied />;

  return <Outlet />;
}
```

## 3. Layout dinâmico por modo

```jsx
// src/App.jsx — o layout é escolhido pela rota, o conteúdo muda com o modo
export function App() {
  return (
    <Routes>
      <Route element={<PublicLayout />}>
        <Route path="/login" element={<LoginPage />} />
      </Route>

      <Route element={<ProtectedRoute mode="contracting" />}>
        <Route element={<ContractingLayout />}>
          {contractingRoutes.map((r) => (
            <Route key={r.path} path={r.path}
                   element={r.permission
                     ? <RequirePermission permission={r.permission}>{r.element}</RequirePermission>
                     : r.element} />
          ))}
        </Route>
      </Route>

      <Route element={<ProtectedRoute mode="provider" />}>
        <Route element={<ProviderLayout />}>
          {providerRoutes.map(/* idem */)}
        </Route>
      </Route>

      <Route element={<ProtectedRoute mode="global" />}>
        <Route element={<PlatformLayout />}>
          {platformRoutes.map(/* idem */)}
        </Route>
      </Route>

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
```

**Quando o modo muda** (`switch-mode` devolve novo token): `AccessContext` atualiza
`activeMode` → `ProtectedRoute` reavalia → `Navigate` para a home do novo modo →
`AppShell` (topnav/sidebar) re-renderiza com o layout do modo ativo.

## 4. Sidebar dinâmico (renderiza só o que o usuário pode)

```jsx
// src/components/layout/Sidebar.jsx
const NAV = {
  contracting: [
    { to: '/app/contracting/requisitions', label: 'Vagas', icon: 'bi-briefcase',
      permission: 'recruitment.requisition.manage' },
    { to: '/app/contracting/proposals/inbox', label: 'Propostas', icon: 'bi-inbox',
      permission: 'proposals.decide' },
    { to: '/app/contracting/invoices', label: 'Faturas', icon: 'bi-receipt',
      permission: 'financial.invoice.issue' }
  ],
  provider: [
    { to: '/app/provider/projects', label: 'Projetos', icon: 'bi-grid', permission: 'jobs.project.apply' },
    { to: '/app/provider/proposals', label: 'Minhas propostas', icon: 'bi-send', permission: 'proposals.submit' },
    { to: '/app/provider/balance', label: 'Saldo e saques', icon: 'bi-wallet2', permission: 'financial.payout.manage' }
  ]
};

export function Sidebar() {
  const { activeMode, can } = useAccess();
  const items = (NAV[activeMode] ?? []).filter((i) => !i.permission || can(i.permission));

  if (items.length === 0) return null;   // modo ativo sem itens visíveis ⇒ layout simples

  return (
    <nav className="nav flex-column p-3">
      {items.map((i) => (
        <NavLink key={i.to} to={i.to} className="nav-link">
          <i className={`bi ${i.icon} me-2`} />{i.label}
        </NavLink>
      ))}
    </nav>
  );
}
```

## 5. Lazy loading por feature

```jsx
// src/routing/AppRouter.jsx — carrega página sob demanda (bundle menor)
const RequisitionCreatePage = lazy(() => import('../features/recruiting/RequisitionCreatePage.jsx'));

<Suspense fallback={<SkeletonShell />}>
  <Routes>…</Routes>
</Suspense>
```

## 6. Cenários de navegação cobertos

| Cenário | Comportamento |
| ------- | ------------- |
| Login como RECRUITER (só contracting) | redireciona para `/app/contracting/home`; `availableModes = ["contracting"]` |
| Usuário CLIENT+PROVIDER | pode alternar; `ModeSwitcher` mostra os dois; cada modo tem layout próprio |
| Acesso direto à URL de outro modo | guard redireciona para home do modo ativo (se disponível) ou 403 |
| Token expira no meio da sessão | refresh single-flight; falha → login |
| Permissão revogada (sessão ativa) | backend responde 403 → UI mostra `PermissionDenied`; `/me` recarregado atualiza o espelho |
| SUPER_ADMIN | contexto global (`/platform/*`), sem tenant na URL |