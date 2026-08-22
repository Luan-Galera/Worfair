# 04 — Saúde, Logs e Observabilidade

## 1. Health checks (API ASP.NET Core)

```csharp
// src/Api/Worfair.Api/Program.cs (resumo)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("identity_db")
    .AddDbContextCheck<FinancialDbContext>("financial_db")
    .AddCheck<RabbitMqHealthCheck>("rabbitmq")
    .AddCheck<OutboxLagHealthCheck>("outbox_lag");          // atraso na entrega do outbox

// Endpoints
app.MapHealthChecks("/health", new HealthCheckOptions           // liveness (rápido)
{
    ResponseWriter = HealthCheckResponseWriters.Minimal
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions     // readiness (dependências)
{
    Predicate = _ => true,                                     // todos os checks
    ResponseWriter = HealthCheckResponseWriters.Json           // status por módulo
});
```

| Endpoint | Uso | Falha |
| -------- | --- | ----- |
| `/health` | liveness (orquestrador) | processo morto/loop |
| `/health/ready` | readiness (tráfego) | DB, RabbitMQ ou outbox atrasado (> N min) |

> Em produção, `UseHealthChecks` não pode depender de credenciais de módulo —
> os checks usam a conexão de leitura da app (`worfair_app`), nunca a do migrator.

## 2. Logs estruturados

- `ILogger` com **formatter JSON** (Serilog ou `AddJsonConsole`) — campos fixos:
  `timestamp`, `level`, `message`, `correlation_id` (do `traceparent`), `tenant_id`,
  `user_id`, `module`, `service`.
- **Nunca** logar: senhas, tokens, dados bancários, payloads de webhook íntegros
  (logar apenas `event_id`/`event_type` — SEC-04).
- Redação automática por `LogEnricher` quando `tenant_id`/`user_id` presentes.
- Nível de log por ambiente: `Information` (dev), `Warning+` (prod) com
  `Debug` sob demanda (feature flag).

## 3. OpenTelemetry (traces + métricas)

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddNpgsql()                       // spans por comando SQL
        .AddMassTransitInstrumentation()   // mensagens RabbitMQ
        .AddSource("Worfair.Modules.*"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddMeter("Microsoft.AspNetCore.Hosting")
        .AddMeter("MassTransit"))
    .UseOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));  // OTLP → coletor
```

Métricas de negócio (módulo Financial — acompanhar a máquina de estados):
- `worfair_financial_transactions_total{status}` (gauge por estado)
- `worfair_financial_transition_illegal_total` (transições rejeitadas — alerta)
- `worfair_outbox_pending_age_seconds` (p95 — alerta de backlog)
- `worfair_webhook_inbox_lag_seconds` (atraso Asaas→processado)

## 4. Stack de observabilidade (staging/produção)

```
App (OTel) ──► OpenTelemetry Collector ──► Prometheus (métricas)
    │                    │                ├─ Loki (logs)
    │                    └────────────────┴─ Tempo (traces)
    └────────────────────────────────────────► Grafana (dashboards + alertas)
```

Fragmento de dev (docker-compose.obs.yml, perfil opcional):

```yaml
services:
  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel-collector.yml"]
    volumes: ["./dev/otel/otel-collector.yml:/etc/otel-collector.yml:ro"]
    ports: ["4317:4317"]
  grafana:
    image: grafana/grafana:latest
    ports: ["3000:3000"]
    volumes: ["./dev/grafana/provisioning:/etc/grafana/provisioning:ro"]
  prometheus:
    image: prom/prometheus:latest
    ports: ["9090:9090"]
```

## 5. Dashboards e alertas essenciais

| Alerta | Condição | Ação |
| ------ | -------- | ---- |
| Error rate de API | `5xx / total > 1%` em 5min | pager (P1) |
| Outbox atrasado | `outbox_pending_age > 10min` | P2 — pode parar de processar pagamentos |
| DLQ com mensagens | fila de erro do RabbitMQ > 0 por 15min | P2 (evento financeiro não processado) |
| Transição ilegal | `transition_illegal_total` incrementa | P1 — possível bug/atack |
| Webhook inbox cresce | `webhook_inbox_lag > 5min` | P2 |
| DB connections | `max_connections` acima de 80% | P2 |
| Latência p95 > 800ms | por rota | P2 |
| RabbitMQ dead letters | fila `worfair.financial.dlx` | P2 |

Logs de auditoria (`audit.audit_logs`) permanecem no PostgreSQL (append-only,
SEC-04) — **não** são roteados para Loki; Loki guarda logs operacionais.

## 6. Ciclo de vida do request (correlação)

`traceparent` gerado no browser (frontend) → repassado pela API (header) →
spans por handler/camada/mensageria → console/Grafana. Um único `correlation_id`
do erro de pagamento até o webhook do Asaas.