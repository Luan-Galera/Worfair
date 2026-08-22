# Worfair — Módulo Financeiro (Asaas + RabbitMQ)

Documento de referência oficial da integração financeira com **Asaas (Sandbox)**
e **RabbitMQ**, complementar a `docs/architecture/`, `docs/database/` e
`docs/security/`. **Alinhado** a D-01..D-11, R-01..R-12, DB-01..DB-08 e
SEC-01..SEC-04; novos deltas na seção 2.

## 1. Índice

| Doc | Tema |
| --- | ---- |
| [01 — Fluxo de pagamento com Asaas Payment Split](01-fluxo-pagamento-asaas-split.md) | Divisão automática de valores entre contas/wallets Asaas, retenção operacional (sem escrow jurídico) |
| [02 — Máquina de Estados Financeira](02-maquina-de-estados-financeira.md) | Transições explícitas, permitidas e proibidas, com validação de domínio + banco |
| [03 — Webhooks Asaas × RabbitMQ](03-webhooks-asaas-rabbitmq.md) | Topologia assíncrona: ingestão, filas, DLQ, processamento confirmado |
| [04 — Resiliência: Idempotência e Outbox](04-resiliencia-idempotencia-outbox.md) | Webhook inbox, at-least-once, Outbox transacional, trilha de auditoria imutável |
| [05 — Exemplo: Consumer MassTransit](05-exemplo-consumer-mass-transit.md) | Consumer idempotente + Outbox em C# (código completo) |

## 2. Atualizações de continuidade (v1.3)

| # | Mudança | Base anterior | Motivo |
| - | ------- | ------------- | ------ |
| DB-09 | Novas tabelas `financial.transactions`, `financial.transaction_events`, `financial.asaas_webhook_inbox`, `financial.transfers` | schema financial (v1.1) | Suporte ao ciclo de vida Asaas + Split + retenção |
| FIN-01 | **Asaas Payment Split** como mecanismo de divisão; **retenção operacional** (valor permanece na conta Asaas da plataforma até a liberação) — Asaas **não** fornece escrow jurídico | escrow_transactions (conceitual) | Capacidade real da API Asaas |
| FIN-02 | **Máquina de estados financeira explícita** (`PENDING → … → RELEASED`) validada no domínio **e** no banco (função `SECURITY DEFINER`) | transições implícitas | Requisito explícito |
| FIN-03 | **RabbitMQ + MassTransit** para webhooks Asaas, com **EF Outbox transacional** | outbox próprio (arquitetura D-07) | Requisito explícito (MassTransit outbox é a implementação de referência) |
| FIN-04 | Webhooks Asaas são **confirmados no provedor** (GET `/payments/{id}`) antes de aplicar transição — payload nunca é fonte de verdade | — | Segurança contra payload falsificado/replay |

## 3. Decisões de integração (resumo)

| Item | Decisão |
| ---- | ------- |
| Provedor | Asaas — **Sandbox** (`https://sandbox.asaas.com/api/v3`), chave `access_token` |
| Conta dona do pagamento | Conta Asaas **da plataforma** (por ambiente) — todos os tenants faturam nela |
| Split | `split[]` na criação do pagamento: `walletId` do prestador (`percentualValue`) + comissão da plataforma (`fixedValue`) |
| Retenção | Operacional: parte do valor fica na conta da plataforma até entrega concluída; liberação via **Transfer Asaas** (`TRANSFER_DONE`) |
| Mensageria | RabbitMQ topic exchange `worfair.financial.events` + DLQ; consumidores com retry e ack pós-commit |
| Outbox | MassTransit `AddEntityFrameworkOutbox` (Postgres) — publicação só após commit da transação financeira |
| Segurança | Tabelas financeiras append-only (SEC-04) + RLS (R-02) + funções de transição `SECURITY DEFINER` |

## 4. Regras de continuidade para prompts futuros

1. Nenhum endpoint altera status financeiro diretamente — sempre via máquina de
   estados (domínio + função de banco).
2. Nenhum webhook é aplicado sem confirmação no provedor (GET no Asaas).
3. Toda transição gera evento imutável (`transaction_events`) na mesma transação.
4. Publicações em RabbitMQ saem **somente** do Outbox (nunca publish direto no
   handler).
5. `PENDING → PAYMENT_CREATED → PAYMENT_RECEIVED → FUNDS_AVAILABLE →
   RELEASE_REQUESTED → RELEASED` é a espinha dorsal; estados de falha
   (`PAYMENT_FAILED`, `PAYMENT_REFUNDED`, `RELEASE_FAILED`) são terminais ou
   reentrantes apenas onde a matriz permite.