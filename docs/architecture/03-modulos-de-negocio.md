# 03 — Módulos de Negócio

Cada módulo é um **bounded context** com modelo, banco (schema) e contratos próprios.
Nenhuma tabela é compartilhada entre módulos; referências cruzadas são feitas por ID.

## 1. Mapa dos módulos

| Módulo | Bounded context | Responsabilidade | Schema SQL | Agregados principais |
| ------ | --------------- | ---------------- | ---------- | -------------------- |
| **Identity** | Acesso & autorização | Usuários, roles, permissões (RBAC), autenticação (JWT), MFA, refresh tokens | `identity` | `User`, `Role`, `Permission` |
| **Tenants** | Organização | Ciclo de vida do tenant, planos/tiers, configurações, branding, feature flags, endereço | `tenancy` | `Tenant`, `TenantSettings`, `Subscription`, `FeatureFlag` |
| **Jobs** | Oportunidades | Publicação de **vagas no marketplace** e **projetos/serviços**, categorias, skills, candidatura pública | `jobs` | `JobPosting`, `ServiceProject`, `Skill`, `Category` |
| **Recruitment** | Processo seletivo (ATS) | Requisições de vaga, candidatos, pipeline, entrevistas, avaliações, time de contratação, talent pool | `recruitment` | `JobRequisition`, `Candidate`, `Interview`, `Assessment`, `HiringTeam` |
| **Proposals/Hiring** | Contratação & oferta | Propostas de serviço, negociação, oferta (offer letter), aceite, decisão de contratação, contrato inicial | `proposals` | `Proposal`, `Offer`, `HiringDecision`, `Contract` |
| **Financial** | Dinheiro | Faturamento, pagamentos, escrow/retenção, payouts, comissões, cobrança de planos | `financial` | `Invoice`, `Payment`, `EscrowTransaction`, `Payout`, `BillingCycle` |
| **Notifications** | Comunicação | E-mail, SMS, push, in-app; templates; preferências; entrega (consome eventos dos demais) | `notifications` | `Notification`, `NotificationTemplate`, `NotificationPreference` |

## 2. Fronteiras entre módulos (regras de negócio)

### 2.1 Identity × Tenants

- `User` é **global** (identidade da pessoa). A filiação a um tenant é a relação
  `TenantMembership` no módulo Tenants (user → tenant, role no tenant, status ativo).
- Permissões globais (ex.: `platform.manage`) vs permissões por tenant (ex.:
  `recruitment.requisition.publish`) são dois espaços distintos.
- Autenticação valida usuário + tenant; **autorização** valida role/permissão
  **dentro do tenant do token**.

### 2.2 Jobs × Recruitment

- `JobPosting` (Jobs) = **vitrine**: o que o candidato vê e onde se candidata.
- `JobRequisition` (Recruitment) = **operação interna**: o processo seletivo.
- Fluxo: `JobPosting.Published` (Jobs) → cria `JobRequisition` (Recruitment).
  Uma requisição pode existir sem posting (contratação interna).
- Recruitment **nunca** escreve tabelas de Jobs; lê por contrato read-only.

### 2.3 Recruitment × Proposals/Hiring

- Recruitment termina em `CandidateHired` (escolha do candidato vencedor).
- Proposals/Hiring assume dali: `Offer` (oferta formal), negociação, aceite, `Contract`.
- Em serviços (freelance), o fluxo é inverso: `Proposal` nasce do candidato/provedor
  para um `ServiceProject` (Jobs); a aceitação da proposta gera `Contract`.

### 2.4 Recruitment × Financial

- Salário/faixa salarial nasce no Recruitment (`SalaryRange`), mas **valores
  faturados/pagos** (holerite, payout, comissão) são Financial. Nada de financeiro
  em tabelas de Recruitment.

### 2.5 Notifications × todos

- Notifications é consumidor puro. Os demais módulos **publicam** integration events;
  Notifications escuta e envia. **Nenhum** módulo conhece transporte de e-mail/SMS/push.

## 3. Módulo Recruitment em detalhe

### 3.1 Papéis e autorização por tenant (catálogo oficial v1.2 — DB-07)

| Role (código) | Escopo | Ações principais |
| ----- | ------ | ---------------- |
| `SUPER_ADMIN` | **Global** (sem tenant) | administração da plataforma (`platform.*`) |
| `OWNER` | Tenant | controle total do tenant: settings, membros, decisões |
| `CLIENT` | Tenant | contratante: projetos, decisão de propostas, ofertas |
| `RECRUITER` | Tenant | gerencia requisições, pipeline, avança candidatos, agenda entrevistas |
| `HIRING_MANAGER` | Tenant | dono da requisição, aprova faixa salarial, decisão final de contratação |
| `PROVIDER` | Tenant | prestador: propostas, execução de serviços, recebimento de pagamento |

A autorização é **composição de permissões efetivas + tenant**: o mesmo usuário
pode ser `RECRUITER` no tenant A e `PROVIDER` no tenant B, e **acumular múltiplas
roles no mesmo tenant** (`user_roles` N:N — ex.: `OWNER` + `RECRUITER` +
`HIRING_MANAGER`). Papéis finos (Sourcer, Interviewer) são expressos como
**permissões** da role `RECRUITER` — não como roles próprias.

> **Modos operacionais (DB-08/SEC-03):** *Modo Contratante* e *Modo Prestador*
> **não são roles** — são contextos derivados das permissões efetivas do usuário
> (Contratante: `CLIENT`/`OWNER`/`RECRUITER`/`HIRING_MANAGER`; Prestador:
> `PROVIDER`). Ver [docs/security/03 — Políticas de autorização](../../docs/security/03-politicas-autorizacao.md).

### 3.2 Agregados e seus invariantes

**`JobRequisition`** (Aggregate Root)
- Status (máquina de estados): `Draft → Published ⇄ Paused → Closed/Cancelled`.
- Invariantes:
  - Não publica sem **time de contratação** definido.
  - `SalaryRange.Min <= SalaryRange.Max` (mesmo currency).
  - Não altera faixa salarial quando `Closed`/`Cancelled`.
  - Um mesmo `RecruiterId` não se repete no time.
- Domain Events: `JobRequisitionCreated`, `JobRequisitionPublished`,
  `JobRequisitionClosed`.

**`Candidate`** (Aggregate Root)
- Status: `Sourced → Applied → Screened → Interviewing → Offered → Hired/Rejected`.
- Invariantes:
  - `ContactEmail` único por tenant (não global — dois tenants podem ter o mesmo e-mail).
  - Transições de status só seguem a máquina definida.
  - Não pode avançar para `Interviewing` sem entrevista agendada.
- Domain Events: `CandidateApplied`, `CandidateAdvanced`,
  `CandidateHired` (dispara `CandidateHiredIntegrationEvent` → Proposals/Hiring).

**`Interview`** (Aggregate Root)
- Invariantes: horário dentro do horário comercial do tenant (settings), pelo menos
  um `Interviewer` do `HiringTeam`, feedback obrigatório para concluir.

### 3.3 Eventos publicados/consumidos pelo Recruitment

**Publica:**
- `JobRequisitionPublished` → Notifications (divulgar), Financial (não se aplica ainda).
- `CandidateHired` → Proposals/Hiring (criar Offer), Notifications (parabenizar).

**Consome:**
- `JobPostingPublished` (Jobs) → cria `JobRequisition` em `Draft`.
- `UserProvisioned` (Identity/Tenants) → habilita acesso ao módulo.

## 4. Catálogo de integration events (v1)

| Evento | Origem | Destino | Propósito |
| ------ | ------ | ------- | --------- |
| `UserProvisioned` | Identity | Todos | disponibiliza usuário para os módulos |
| `TenantProvisioned` | Tenants | Todos | cria contexto default do tenant |
| `JobPostingPublished` | Jobs | Recruitment | cria requisição em Draft |
| `JobPostingClosed` | Jobs | Recruitment | pausa requisições vinculadas |
| `CandidateHired` | Recruitment | Proposals, Notifications | inicia fluxo de oferta |
| `OfferAccepted` | Proposals | Financial, Notifications | gera contrato + faturamento inicial |
| `ContractSigned` | Proposals | Financial, Jobs | inicia entrega/escrow |
| `PaymentReceived` | Financial | Notifications, Proposals | atualiza status de pagamento |
| `TaskAssigned` | Jobs | Notifications | notifica envolvidos |

## 5. Regras de consistência

1. **Saga/process manager** quando a transação cruza módulos (ex.: `OfferAccepted` →
   `Contract` + `Invoice`). O Outbox garante *at-least-once*; handlers devem ser
   **idempotentes** (deduplicação por `IntegrationEvent.Id`).
2. Eventos de negócio não carregam dados sensíveis; só IDs e metadados mínimos.
3. Consulta cruzada (leitura) nunca faz join entre módulos — usa contratos read-only
   ou projeções materializadas (read models) atualizadas por eventos.