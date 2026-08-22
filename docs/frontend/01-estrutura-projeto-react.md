# 01 — Organização do Projeto React

## 1. Stack e convenções (FE-01)

| Item | Escolha |
| ---- | ------- |
| Build | Vite (React plugin), `npm` |
| Linguagem | **JavaScript** (JSX) — sem TypeScript |
| UI | **Bootstrap 5** (SCSS customizado) + `bootstrap-icons` |
| Router | React Router v7 |
| HTTP | Axios (instância única + interceptors) |
| Estado | Context API + hooks (auth, access) — sem biblioteca externa nesta fase |
| Lint/Format | ESLint + Prettier |

## 2. Árvore de pastas

```
worfair-web/
├── index.html
├── package.json
├── vite.config.js                  # plugin react + proxy /api → backend em dev
├── .env.development                # VITE_API_URL=/api (proxy) — SEM segredos
├── .env.production                 # VITE_API_URL=https://api.worfair.app
├── public/
│   └── favicon.svg
└── src/
    ├── main.jsx                    # bootstrap imports + ThemeProvider + Router
    ├── App.jsx                     # providers: Auth → Access → Router
    ├── api/
    │   ├── http.js                 # instância Axios + interceptors (JWT, refresh, 403)
    │   └── endpoints/
    │       ├── auth.js             # login, refresh, switch-mode, switch-tenant
    │       ├── identity.js         # /me (permissions, modes, memberships)
    │       ├── recruitment.js      # requisitions, candidates, interviews
    │       ├── jobs.js             # postings, projects
    │       ├── proposals.js        # proposals, offers
    │       ├── financial.js        # invoices, balance, payouts, statements
    │       └── notifications.js
    ├── auth/
    │   ├── tokenStorage.js         # access/refresh (memória p/ access; refresh: httponly?)
    │   ├── AuthContext.jsx         # user, tokens, login/logout/refresh
    │   └── useAuth.js
    ├── access/
    │   ├── AccessContext.jsx       # permissions, availableModes, activeMode, switchMode
    │   └── useAccess.js
    ├── components/
    │   ├── ui/                     # primitivas reutilizáveis (Bootstrap + acessíveis)
    │   │   ├── Button.jsx
    │   │   ├── Card.jsx
    │   │   ├── Modal.jsx
    │   │   ├── Badge.jsx
    │   │   ├── FormField.jsx       # input + label + help + validação inline
    │   │   ├── Table.jsx
    │   │   ├── MoneyText.jsx       # formatação BRL/ISO 4217
    │   │   ├── StatusBadge.jsx     # mapeia status do domínio → badge
    │   │   └── Stepper.jsx         # wizard (criação de vaga/proposta)
    │   ├── layout/
    │   │   ├── AppShell.jsx        # topnav + sidebar + outlet (por modo)
    │   │   ├── TopNav.jsx          # brand, nav por permissão, ModeSwitcher, UserMenu
    │   │   ├── Sidebar.jsx         # menu filtrado por permissions (nunca renderiza o que não pode)
    │   │   ├── ModeSwitcher.jsx    # alternância Contratante ↔ Prestador (doc 05)
    │   │   ├── TenantSwitcher.jsx  # lista memberships do /me → switch-tenant
    │   │   └── UserMenu.jsx
    │   └── feedback/
    │       ├── EmptyState.jsx
    │       ├── ErrorState.jsx
    │       ├── Skeleton.jsx
    │       └── PermissionDenied.jsx  # 403 com CTA "trocar de modo"
    ├── features/                   # feature-first, espelhando os módulos do backend
    │   ├── auth/
    │   │   ├── LoginPage.jsx
    │   │   └── LoginForm.jsx
    │   ├── tenancy/
    │   │   ├── MembersPage.jsx
    │   │   └── SettingsPage.jsx
    │   ├── recruiting/
    │   │   ├── RequisitionCreatePage.jsx    # wizard (doc 04)
    │   │   ├── RequisitionDetailPage.jsx
    │   │   ├── CandidatePipelinePage.jsx    # kanban
    │   │   └── InterviewsPage.jsx
    │   ├── jobs/
    │   │   ├── JobPostingsPage.jsx
    │   │   └── ProjectsPage.jsx             # marketplace (provider)
    │   ├── proposals/
    │   │   ├── ProposalCreatePage.jsx       # envio de proposta (provider)
    │   │   ├── ProposalsInboxPage.jsx       # decisão (contratante)
    │   │   └── OffersPage.jsx
    │   ├── financial/
    │   │   ├── BalancePage.jsx              # saldo/retenção/extrato
    │   │   ├── PayoutRequestPage.jsx        # saque
    │   │   └── InvoicesPage.jsx
    │   └── notifications/
    │       └── NotificationsPage.jsx
    ├── layouts/
    │   ├── PublicLayout.jsx        # login, marketing, sem auth
    │   ├── ContractingLayout.jsx   # painel do Modo Contratante
    │   ├── ProviderLayout.jsx      # painel do Modo Prestador
    │   └── PlatformLayout.jsx      # SUPER_ADMIN (contexto global)
    ├── routing/
    │   ├── AppRouter.jsx           # <Routes> + lazy loading por feature
    │   ├── routeConfig.js          # tabela de rotas × modo × permissão × layout
    │   ├── ProtectedRoute.jsx      # guard composto (auth → modo → permissão)
    │   └── RequirePermission.jsx   # guard em nível de elemento
    ├── hooks/
    │   ├── useQuery.js             # fetch + loading/error simples
    │   └── useDebounce.js
    ├── utils/
    │   ├── money.js                # Intl.NumberFormat pt-BR
    │   ├── dates.js
    │   └── validators.js           # espelha regras do domínio (salary range, currency)
    └── styles/
        ├── theme.scss              # variáveis Bootstrap customizadas (brand)
        └── main.scss
```

## 3. Camada de API (Axios + interceptors)

```js
// src/api/http.js
import axios from 'axios';
import { getAccessToken, getRefreshToken, persistTokens, clearTokens } from '../auth/tokenStorage';

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  timeout: 15_000
});

// Request: anexa o JWT — e NADA mais (nenhum X-Tenant-Id, nunca — SEC-02)
http.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Response: refresh single-flight + retry exato; 403 sinaliza falha de contexto
let refreshPromise = null;

const IS_REFRESH_ROUTE = (url = '') => url.includes('/identity/refresh');

http.interceptors.response.use(
  (res) => res,
  async (error) => {
    const { config, response } = error;

    if (response?.status === 401 && !config._retried && !IS_REFRESH_ROUTE(config.url)) {
      config._retried = true;

      if (!refreshPromise) {
        refreshPromise = http
          .post('/identity/refresh', { refreshToken: getRefreshToken() })
          .then(({ data }) => { persistTokens(data); return data.accessToken; })
          .catch((err) => {
            clearTokens();
            window.dispatchEvent(new Event('auth:expired'));
            throw err;
          })
          .finally(() => { refreshPromise = null; });
      }

      try {
        const token = await refreshPromise;
        config.headers.Authorization = `Bearer ${token}`;
        return http(config);
      } catch (refreshErr) {
        return Promise.reject(refreshErr);
      }
    }

    if (response?.status === 403) {
      // O backend negou: contexto (tenant/modo) ou permissão. A UI não decide —
      // apenas orienta (ex.: "trocar de modo" dispara switch-mode e revalida o /me).
      window.dispatchEvent(new CustomEvent('auth:forbidden', { detail: response.data }));
    }

    return Promise.reject(error);
  }
);

// Endpoints por feature (ex.: src/api/endpoints/auth.js)
export const authApi = {
  login: (payload) => http.post('/identity/login', payload),
  switchMode: (mode) => http.post('/identity/switch-mode', { mode }),
  switchTenant: (tenantId) => http.post('/identity/switch-tenant', { targetTenantId: tenantId }),
  me: () => http.get('/identity/me')
};
```

**Regras da camada de API:**
1. O frontend **nunca** adiciona `X-Tenant-Id`, `tenant_id` ou `mode` como
   header/body de requisições de negócio — o backend lê do token (SEC-02/FE-02).
2. A única exceção: `switch-mode`/`switch-tenant` (intenção explícita de
   troca, validada pelo servidor).
3. `401` → refresh rotativo single-flight com **retry exato** da requisição
   original (`_retried`, sem loop no próprio `/identity/refresh`); falha de
   refresh → `auth:expired` (logout limpo, sem loop de redirect). `403` →
   evento `auth:forbidden` (a UI pergunta se o usuário quer trocar de modo/tenant
   — não tenta "consertar" por conta própria).
4. **Por que não há fila `pending`:** o single-flight (`refreshPromise`)
   já faz todos os `401` concorrentes aguardarem o **mesmo** refresh e
   retentarem com o token novo; um array de callbacks só adicionaria estado
   morto e risco de execução dupla (correção FE-05).

## 4. Convenções

- **Feature-first:** tudo relacionado a uma capacidade mora em `features/<nome>/`;
  componentes genéricos em `components/`.
- **Endpoints centralizados:** páginas não instanciam Axios — sempre `endpoints/*`.
- **Formatação:** dinheiro sempre via `utils/money.js` (BRL por padrão, ISO 4217
  quando o payload indicar outra moeda).
- **Status do domínio:** exibido via `StatusBadge` com a mesma codificação de
  cores dos enums do backend (PENDING → gray, RECEIVED → green, REFUNDED → red…).
- **Acessibilidade:** foco visível, labels obrigatórios, `aria-live` em toasts e
  modais, contraste WCAG AA.