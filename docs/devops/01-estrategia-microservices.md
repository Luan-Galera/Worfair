# 01 — Monolito Modular → Microsserviços

## 1. Posição de princípio

O Monolito Modular **já é a preparação** para microsserviços: as fronteiras de
módulo definidas em `docs/architecture/` (Domain/Application/Infrastructure +
Contracts + composition root) são exatamente as futuras fronteiras de serviço.
**Não há trabalho de refatoração estrutural prévio** — há trabalho de *empacotamento*.

## 2. Decisões atuais que tornam a extração barata

| Decisão | Por que ajuda |
| ------- | ------------- |
| Módulos com camadas próprias e composition root isolado (`AddRecruitment()`) | o módulo já é "autossuficiente" em DI |
| Comunicação por **Integration Events** + Outbox (D-07) | trocar o transporte in-process por RabbitMQ não muda contratos |
| Contratos `*.Contracts` (eventos + portas read-only) | superfície pública já está definida e versionável |
| **DbContext por módulo** + schema próprio (D-04/D-08) | extração de banco é "mudar a connection string" |
| Referências entre módulos **por ID**, sem FK (R-07) | split de banco sem violar integridade |
| Idempotência em consumidores (FIN/D-07) | reentrega via rede é transparente |
| Append-only financeiro + máquina de estados (SEC-04/FIN-02) | invariantes sobrevivem à separação |

## 3. Gatilhos para extrair (não antes)

- Escala: times independentes (> ~15 devs) ou carga que exige scaling distinto
  (ex.: Notifications escala sozinho).
- Conformidade: exigência de isolamento físico de dados por módulo.
- Falha isolável: necessidade de blástio contido por domínio.
- **Nunca** extrair por dogma: cada serviço novo paga custo operacional real.

## 4. Plano de extração em fases

### Fase 0 — Congelar contratos
- Semver explícita nos `IntegrationEvents` (ex.: `v1`), schema registry.
- Testes de arquitetura (NetArchTest) garantindo fronteiras no CI.
- Garantir que **nenhum** código fora do módulo referencia tipos internos.

### Fase 1 — Extrair o processo (mais barato e seguro)
- O módulo vira um **novo host** (`Worfair.Services.Recruitment`) usando o mesmo
  composition root (`AddRecruitment()`), mesmo DbContext e mesmas migrations.
- O host da API passa a não registrar esse módulo (config flag).
- Transporte de integration events migra de in-process para RabbitMQ (JÁ é o
  caso do Financial — padronizar os demais).
- **Paridade:** testes E2E rodando contra ambos os topologias no CI (verificação
  de comportamento idêntico — *strangler*).

### Fase 2 — Extrair o banco
- Schema do módulo migra para **banco próprio** (mesmo cluster ou dedicado):
  a abstração `ITenantDbContextAccessor` já isola a resolução de conexão
  (docs/database/04, seção 5).
- Migrations do módulo passam a rodar contra o novo banco (docs/devops/02).
- Relatórios cross-módulo: substituir joins por **read models** atualizados por
  eventos (D-07) — já previsto.

### Fase 3 — Escalar e isolar
- Réplicas de leitura, `max_connections` dedicado, quotas por serviço.
- Rate limiting/BFF: gateway (ex.: YARP) roteia `/api/*` para o serviço certo.
- Observabilidade por serviço (docs/devops/04) + SLIs/SLOs por módulo.

## 5. O que NÃO fazer

- ❌ Não dividir tabelas de um módulo entre serviços (a fronteira é o módulo).
- ❌ Não usar transações distribuídas entre módulos (saga/outbox já definidos).
- ❌ Não compartilhar DbContext entre módulos "por enquanto" (quebra R-07).
- ❌ Não extrair dois módulos ao mesmo tempo (um por vez, com janela de paridade).

## 6. Critérios de saída da Fase 1 (definition of done)

- [ ] Nenhuma referência direta entre módulos no código (verificado no CI).
- [ ] 100% dos integration events trafegando via RabbitMQ (outbox transacional).
- [ ] E2E idêntico (monolito × distribuído) em ao menos 3 fluxos críticos
      (login+tenant, pagamento Asaas, contratação+oferta).
- [ ] Rollback do serviço extraído é trivial (feature flag de composição).