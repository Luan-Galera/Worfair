# Worfair — Frontend (React + Vite + JavaScript + Bootstrap)

Documento de referência oficial da camada de interface, complementar a
`docs/architecture/`, `docs/database/`, `docs/security/` e `docs/financial/`.
**Alinhado** a D-01..D-11, R-01..R-12, SEC-01..SEC-04 e FIN-01..FIN-04;
deltas na seção 2.

## 1. Índice

| Doc | Tema |
| --- | ---- |
| [01 — Organização do Projeto React](01-estrutura-projeto-react.md) | Pastas modulares, componentes reutilizáveis, camada de API (Axios + interceptors) |
| [02 — Validação de Tenant (Backend-Centric)](02-validacao-tenant-backend-centric.md) | O frontend nunca define contexto; informa intenção, o backend decide |
| [03 — Rotas Protegidas e Layouts Dinâmicos](03-rotas-protegidas-layouts-dinamicos.md) | Roteamento por Modo, guards de permissão, troca dinâmica de layout |
| [04 — UX/UI para Conversão e Usabilidade](04-ux-ui-conversao-usabilidade.md) | Criação de vagas, propostas, painel do recruiter, painel do provider, saldos/saques |
| [05 — Exemplo: Alternância de Modos](05-exemplo-mode-switcher.md) | Componente React + Bootstrap integrado ao estado de permissões |

## 2. Atualizações de continuidade (v1.5)

| # | Mudança | Base anterior | Motivo |
| - | ------- | ------------- | ------ |
| FE-01 | **Stack frontend definida**: React + Vite + **JavaScript** (JSX) + Bootstrap 5 + React Router + Axios | não havia decisão de frontend | Definição oficial do ecossistema |
| FE-02 | **Frontend nunca envia `X-Tenant-Id`** (nem tenant em body/query) — espelha SEC-02; troca de tenant/modo só por endpoints de Identity que emitem novo token | SEC-02 (backend) | Aplicação no cliente |
| FE-03 | **Modo é contexto do token**: alternância Contratante ↔ Prestador chama `POST /identity/switch-mode`; o backend decide e reemite token; a UI apenas espelha `GET /identity/me` | SEC-03 (backend) | Aplicação no cliente |
| FE-04 | **UI espelha, não autoriza**: permissões/modos exibidos vêm do `/me` (cálculo servidor); a UI esconde recursos, mas **toda** requisição continua protegida pelo backend | princípio "claims não são fonte de verdade" | Aplicação no cliente |
| FE-05 | **Interceptor do Axios corrigido** (v1.5 congelada): refresh single-flight (`refreshPromise`) com retry exato de cada requisição 401 (`_retried`), rota `/identity/refresh` excluída do interceptor (sem loop), e remoção da fila `pending` que nunca reprocessava as requisições originais | interceptor com fila `pending` inerte + risco de loop no refresh | Correção incluída na versão definitiva |

## 3. Regras de continuidade para prompts futuros

1. Nenhum componente adiciona `X-Tenant-Id`, `tenant_id` em payloads ou contexto
   via query string — o token é a única fonte de tenant.
2. Nenhuma tela decide autorização: esconde/redireciona por `permissions` do
   `/me`, mas o backend permanece a única autoridade (403/404 reais).
3. Toda alternância de Modo passa por `switch-mode` (novo token); alternar a UI
   nunca concede nada.
4. Rotas são organizadas por Modo (`/app/contracting/*`, `/app/provider/*`,
   `/platform/*`) com guards compostos (auth → modo → permissão).
5. Stack fixa: React + Vite + JavaScript + Bootstrap 5 (+ React Router + Axios).