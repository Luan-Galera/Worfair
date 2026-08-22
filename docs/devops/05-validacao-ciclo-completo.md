# 05 — Validação do Ciclo Completo

Este documento fecha o planejamento: mostra como todas as camadas
(architecture → database → security → financial → frontend → devops) se
integram num único fluxo de ponta a ponta e fornece o checklist de validação
que deve passar antes de considerar o sistema "pronto para produção".

## 1. Fluxo de ponta a ponta (contratação paga)

```
Browser (React)
   │  POST /identity/login {email, senha}            → JWT (claims: user, tenant, mode)
   ▼
Identity Module (identity/)
   │  valida credenciais (Argon2id, SEC-01)
   │  emite JWT assinado (RS256, chave privada em dev/jwt/ — SEC-01)
   ▼
Browser
   │  GET /jobs/requisitions (sem X-Tenant-Id — FE-02/FE-05)
   │  → interceptor do Axios injeta Authorization; tenant vem SÓ do JWT (SEC-02)
   ▼
API → RequisitionHandler (jobs/)
   │  valida claim tenant × policy (ManageRequisition, SEC-03)
   │  DbContext filtrado por TenantId + RLS FORCE (R-02/R-03) — worfair_app
   ▼
Browser
   │  POST /recruitment/applications  (candidatura)
   ▼
Recruitment Module
   │  domain event → integration event (outbox transacional — D-07)
   ▼
RabbitMQ (worfair.recruitment.events / worfair.financial.events)
   ▼
Financial Module (consumer idempotente — FIN-04)
   │  cria proposta de pagamento no Asaas (split 2 partes — FIN-01)
   │  estado PENDING → PAYMENT_CREATED (append-only, FIN-02)
   ▼
Asaas (sandbox/prod)  →  webhook (event_id + event_type + payload)
   │  → inbox idempotente (asaas_webhook_inbox, UNIQUE(event_id,event_type))
   ▼
WebhookProcessorConsumer
   │  PAYMENT_RECEIVED → valida transições (FIN-02) → FUNDS_AVAILABLE
   ▼
Browser (Prestador)
   │  POST /financial/withdrawals/request  → RELEASE_REQUESTED
   ▼
   │  (job de liberação + confirmação GET no provedor — FIN-03)
   ▼
   RELEASED (ou RELEASE_FAILED/PAYMENT_REFUNDED — trilha completa em audit.financial_events)
```

**Verificação de segurança no fluxo:**
1. JWT é *contexto*, não fonte de verdade — claims de tenant/modo revalidadas
   contra permissões efetivas (SEC-02/SEC-03).
2. `X-Tenant-Id` é rejeitado pelo middleware (SEC-02) — o frontend nunca envia
   o tenant (FE-02).
3. Tabela financeira é append-only com trigger de bloqueio + hash chain (SEC-04).
4. Webhook Asaas idempotente por `(event_id, event_type)` (FIN-04).

## 2. Matriz de validação por camada

| Camada | O que validar | Onde está definido |
| ------ | ------------- | ------------------ |
| **Arquitetura** | fronteiras de módulo, composition root, integration events | docs/architecture/01..05 |
| **Banco** | RLS FORCE em TODA tabela tenant-owned; roles `worfair_app`/`worfair_migrator`; tabelas append-only | docs/database/01..04 |
| **Segurança** | JWT RS256, policies por permissão, modo validado no backend, anti-manipulação de tenant | docs/security/01..05 |
| **Financeiro** | máquina de estados completa, split Asaas, inbox/outbox, confirmação no provedor | docs/financial/01..05 |
| **Frontend** | interceptor corrigido (single-flight), rotas por modo, sem envio de tenant | docs/frontend/01..05 |
| **DevOps** | docker-compose paridade, migrations controladas, CI/CD, observabilidade | docs/devops/01..05 |

## 3. Checklist de validação pré-produção (definition of done)

### A. Segurança
- [ ] Login emite JWT RS256 com claims corretas (user, tenant, mode).
- [ ] `X-Tenant-Id` em request → 400/401 (rejeitado).
- [ ] Troca de tenant/modo via `switch-tenant`/`switch-mode` emite novo token;
      `GET /identity/me` reflete o novo contexto.
- [ ] Usuário sem permissão em tenant → 403 (policy), nunca vazamento de dados.
- [ ] `user_roles` com CHECK de escopo (`is_global` ≠ role tenant) — violação de
      integridade bloqueada.
- [ ] Tokens JWT rotacionáveis via JWKS (sem downtime).

### B. Financeiro
- [ ] Webhook do Asaas com `(event_id, event_type)` repetido → processado 1x.
- [ ] Transição ilegal (ex.: `PAYMENT_CREATED → RELEASED`) → rejeitada + trilha
      de auditoria + alerta.
- [ ] Crash do consumidor → reprocessamento sem duplicação (outbox + inbox).
- [ ] Split calculado corretamente no sandbox (percentual fixo por cliente).
- [ ] Confirmação GET no provedor antes de `RELEASE_REQUESTED → RELEASED`.

### C. Integração de banco
- [ ] Migração roda com `worfair_migrator`; app roda com `worfair_app` (RLS ativo).
- [ ] Query sem filtro de tenant retorna vazio (RLS FORCE bloqueia) — teste de
      regressão.
- [ ] Tabelas financeiras: GRANT restrito + trigger append-only + hash chain.

### D. Frontend
- [ ] 401 → refresh single-flight → retry exato (FE-05).
- [ ] Expiração de sessão → redirect limpo p/ login (sem loop).
- [ ] Rotas por modo renderizam apenas com permissão (RequirePermission).
- [ ] `switch-mode` → novo token → UI atualiza contextos sem reload manual.

### E. DevOps / release
- [ ] `docker compose up -d` sobe api+postgres+rabbitmq+frontend limpos.
- [ ] `docker compose run --rm migrate` aplica migrations e `--verify-migrations`
      confirma versão.
- [ ] Imagens por `sha` (GHCR) — redeploy de imagem anterior = rollback de app.
- [ ] Migração em staging: backup → apply → sanidade; produção com aprovação.
- [ ] `/health/ready` reflete DB+RabbitMQ+outbox; alertas configurados (doc 04).
- [ ] Playwright E2E "smoke" verde em staging E produção (read-only).

## 4. Testes de contrato (contratos de integração)

| Contrato | Teste |
| -------- | ----- |
| IntegrationEvent v1 (recruitment→financial) | schema testado; consumer rejeita versão desconhecida |
| Webhook Asaas | payload fixture real (sandbox) processado end-to-end |
| Auth: `switch-mode`/`switch-tenant` | claims no novo JWT batem com `GET /identity/me` |
| RLS | mesmo SQL executado como `worfair_app` x `worfair_migrator` |

## 5. Riscos remanescentes e mitigação

| Risco | Mitigação |
| ----- | --------- |
| Downtime em migração grande | expand/contract + `CREATE INDEX CONCURRENTLY` + janela de manutenção |
| Outbox atrasado parando pagamentos | alerta p95 + DLQ visível + retry exponencial (MassTransit) |
| Webhook perdido (Asaas sem retry) | reconciliação periódica via GET no provedor (FIN-03) |
| Exploração de manipulação de tenant | middleware rejeita header + claims revalidadas (SEC-02) |
| Falha do RabbitMQ em deploy | filas duráveis + bindings declarados idempotentes no startup |
| Frontend quebrado após redeploy | rollback de imagem + cache-bust por sha |

## 6. Conclusão

Com este fechamento, **todos os seis documentos de planejamento** (arquitetura,
banco, segurança, financeiro, frontend e agora devops) estão alinhados à mesma
estratégia: monolito modular com fronteiras claras, isolamento de tenant
verdadeiro (RLS + JWT contexto), pipeline de pagamentos confiável e
idempotente, e automação de CI/CD com controle humano nos pontos críticos.
A execução pode começar pelos artefatos de dev (docker-compose + migrações) e
pela correção do interceptor do Axios (FE-05).