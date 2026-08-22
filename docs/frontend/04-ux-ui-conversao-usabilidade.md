# 04 — UX/UI para Conversão e Usabilidade

## 1. Princípios de design

1. **Conversão primeiro:** os fluxos de dinheiro (criar vaga → receber
   propostas; enviar proposta → ser contratado; entregar → receber) são wizards
   curtos com progresso visível e próxima ação sempre à vista (CTA primário).
2. **Confiança = visibilidade:** saldo, retenção e histórico financeiro são
   sempre auditáveis (extrato no estilo *ledger*); prazos e regras de liberação
   explicados em linguagem simples.
3. **Usabilidade real:** Bootstrap 5 responsivo (mobile-first), foco visível,
   estados de loading/esvaziamento/erro em toda tela, formulários com validação
   inline que espelha as regras do domínio.
4. **A UI espelha, o backend decide:** nenhum botão "aparece" por heurística
   local — tudo deriva de `permissions` do `/me` (FE-04).

## 2. Tokens de UI (tema Bootstrap)

```scss
// src/styles/theme.scss
$primary: #2563eb;        // ação contratante
$secondary: #7c3aed;      // ação prestador
$success: #16a34a;        // recebido / liberado
$warning: #d97706;        // pendente / retenção
$danger: #dc2626;         // recusado / estorno
$info: #0891b2;
$font-family-base: 'Inter', system-ui, sans-serif;
$border-radius: .5rem;
```

`StatusBadge` mapeia os enums de domínio para cores consistentes:
`PENDING`(gray) → `PAYMENT_CREATED`(info) → `PAYMENT_RECEIVED`(success) →
`FUNDS_AVAILABLE`(success) → `RELEASE_REQUESTED`(warning) → `RELEASED`(success)
→ `PAYMENT_REFUNDED`(danger).

## 3. Criação de vaga (Modo Contratante, RECRUITER/HIRING_MANAGER)

**Objetivo:** publicar uma vaga com zero atrito, sem quebrar as invariantes do
domínio (`SalaryRange`, time de contratação mínimo).

- **Wizard de 3 passos** (`Stepper`): *Dados* → *Remuneração* → *Time e revisão*.
- Validação inline espelhando o domínio: salário mínimo ≤ máximo, mesma
  currency; time com ≥ 1 membro; título ≤ 120 chars.
- Botão "Salvar rascunho" sempre disponível (`Draft` é estado de domínio);
  "Publicar" habilitado só quando o passo 3 confirma pré-requisitos — se faltar
  membro no time, o CTA explica o motivo (não falha no backend).
- Preview público da vaga antes de publicar.
- **Conversão:** barra de progresso, microcópias ("Receba propostas em até 48h"),
  e checklist de onboarding para a 1ª vaga.

## 4. Envio de proposta (Modo Prestador, PROVIDER)

**Objetivo:** transformar um projeto aberto em proposta em < 2 minutos.

- Tela do projeto: requisitos, orçamento indicativo, prazo, skills (chips).
- Card de proposta (`ProposalCreatePage`): valor (currency BRL), mensagem,
  prazo de entrega — com estimativa de "taxa da plataforma" e valor líquido
  mostrada **antes** do envio (conversão por transparência).
- Confirmação com resumo + estado da proposta (Submitted → Negotiating →
  Accepted/Rejected) acompanhado na lista "Minhas propostas".
- Regras do domínio espelhadas: valor > 0; projeto ainda Open; usuário com
  `proposals.submit`.

## 5. Painel do Recruiter (kanban de pipeline)

- **Kanban por estágio** (Sourced → Applied → Screened → Interviewing →
  Offered → Hired/Rejected) com drag-and-drop; mover card = comando
  `candidate.advance` — o backend valida a máquina de estados do Candidate.
- Colunas com contagem e SLA (tempo médio por estágio); card mostra nome,
  estágio, próxima entrevista e badge de risco.
- **Agenda de entrevistas:** calendário semanal, horários no timezone do tenant
  (`tenants.timezone`), botões agendar/registrar feedback (INTERVIEWER).
- **Métricas:** tempo para preencher vaga, fonte dos candidatos, taxa de avanço
  por estágio — com `EmptyState` quando sem dados.

## 6. Painel do Provider (contratos e entregas)

- **Contratos ativos:** status (Signed → Active → Completed → Terminated),
  entregáveis com checkboxes, prazos, botão "Solicitar liberação" desabilitado
  até a regra de negócio (entrega confirmada) — com tooltip explicando por quê.
- **Notificações de eventos** (`financial.*`): pagamento recebido, liberação
  solicitada, liberação concluída — no topo do painel.

## 7. Saldos e saques (transparência financeira)

**Tela `BalancePage`** (permissão `financial.payout.manage`):

```
┌────────────────────────────────────────────────────────────┐
│  Saldo disponível       Retenção em liberação    Histórico │
│  R$ 4.250,00            R$ 1.500,00            (extrato)   │
│  [ Solicitar saque ]    [4 liberações em andamento]         │
└────────────────────────────────────────────────────────────┘
```

- **Três blocos sempre visíveis:** (1) saldo disponível (fundos `RELEASED` ou
  `FUNDS_AVAILABLE` sem solicitação), (2) retenção/em liberação
  (`RELEASE_REQUESTED`, com prazo estimado), (3) extrato (ledger) com
  paginação.
- **Saque (`PayoutRequestPage`):** resumo antes de confirmar (valor, tarifa,
  líquido, destino da wallet Asaas), confirmação explícita, e **estado de
  Pending/Processing/Completed/Failed** acompanhado em tempo real; `Payments`
  imutáveis no backend (SEC-04) — a UI nunca edita histórico.
- **Estorno visualizado** como lançamento de reversão (negativo) no extrato,
  com rótulo "estorno" — nunca como edição de valor.
- Conversão/confiança: data prevista de liberação (T+1 conforme Asaas), FAQ
  inline sobre retenção, e notificação push/in-app em cada mudança de estado.

## 8. Estados de UI obrigatórios

| Estado | Padrão |
| ------ | ------ |
| Carregando | `Skeleton` por seção (nunca spinner full-screen bloqueante) |
| Vazio | `EmptyState` com próxima ação (ex.: "Crie sua primeira vaga") |
| Erro de rede | `ErrorState` com botão "Tentar novamente" |
| Sem permissão | `PermissionDenied` com CTA condicional (trocar de modo se disponível) |
| 404 | tela genérica "Não encontrado" (sem vazar existência cross-tenant) |

## 9. Acessibilidade e performance

- WCAG AA: contraste, foco visível, labels, `aria-live` em toasts, drag-and-drop
  com alternativa via botões (a11y para kanban).
- Lazy loading por rota; ícones via `bootstrap-icons` (font/svg inline);
  `Intl.NumberFormat('pt-BR')` para moeda/datas — sem libs de formatação.
- Testes: `vitest` + `@testing-library/react` (componentes de acesso e guards);
  E2E `playwright` para o fluxo modo → 403 → switch-mode → re-render.