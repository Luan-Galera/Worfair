# 05 — Proteção de Históricos Financeiros e Registros de Auditoria

## 1. Princípios (SEC-04)

1. **Append-only:** registros financeiros e de auditoria são **imutáveis** para
   a aplicação — sem `UPDATE`, sem `DELETE`, sem `TRUNCATE`.
2. **Transições controladas:** mudança de estado (ex.: pagamento capturado)
   acontece por **função dedicada** (`SECURITY DEFINER`) que valida a máquina de
   estados e escreve **eventos** — nunca por edição direta.
3. **Correção por estorno, não por alteração:** erro é corrigido com lançamento
   reverso (novo registro), preservando o histórico.
4. **Auditoria na mesma transação** da operação de negócio (rollback conjunto).
5. **Tempo de servidor:** `occurred_at`/`paid_at` vêm de `now()` do banco —
   nunca de relógio do cliente.

## 2. Camadas de proteção

| Camada | Mecanismo |
| ------ | --------- |
| Banco — GRANTs | `worfair_app` recebe **apenas** `SELECT, INSERT` nessas tabelas (sem UPDATE/DELETE/TRUNCATE) |
| Banco — Triggers | `BEFORE UPDATE OR DELETE` ⇒ `RAISE EXCEPTION` (defesa caso um GRANT seja alterado por engano) |
| Banco — RLS | leitura isolada por tenant (R-02); `WITH CHECK` na escrita |
| Banco — Funções dedicadas | transições de status via `SECURITY DEFINER` com validação de máquina de estados |
| Aplicação | domínio com métodos transicionais (D-10) + interceptor de auditoria |
| Auditoria — hash chain | detecção de adulteração retroativa |

## 3. Tabelas protegidas

| Tabela | Acesso da app | Transições |
| ------ | ------------- | ---------- |
| `financial.payments` | SELECT, INSERT | `transition_payment()` + `payment_events` |
| `financial.transactions` (FIN-02) | SELECT, INSERT | `transition_transaction()` + `transaction_events` (máquina de estados do módulo financeiro) |
| `financial.transaction_events` | SELECT, INSERT | append-only puro |
| `financial.transfers` | SELECT, INSERT | `transition_transfer()` |
| `financial.escrow_transactions` | SELECT, INSERT | `release_escrow()` + evento |
| `financial.payouts` | SELECT, INSERT | `complete_payout()` + evento |
| `financial.invoices` (após Issued) | SELECT, INSERT | `issue_invoice()` congela valores |
| `financial.ledger_entries` | SELECT, INSERT | append-only puro |
| `financial.asaas_webhook_inbox` | SELECT, INSERT | `mark_webhook_processed()`/`mark_webhook_failed()` (claim + status) |
| `audit.audit_logs` | SELECT, INSERT | append-only puro (sem transições) |
| `identity.refresh_tokens` | SELECT, INSERT, UPDATE (revogação) | exceção: `revoked_at` via função |

## 4. SQL de referência

```sql
-- 4.1 Imutabilidade por GRANT (regra primária)
REVOKE UPDATE, DELETE, TRUNCATE ON financial.payments,
    financial.escrow_transactions, financial.payouts,
    financial.invoices, financial.invoice_items,
    financial.ledger_entries
    FROM worfair_app;
GRANT SELECT, INSERT ON financial.payments,
    financial.escrow_transactions, financial.payouts,
    financial.invoices, financial.invoice_items,
    financial.ledger_entries
    TO worfair_app;

REVOKE UPDATE, DELETE, TRUNCATE ON audit.audit_logs FROM worfair_app;
GRANT SELECT, INSERT ON audit.audit_logs TO worfair_app;

-- 4.2 Imutabilidade por trigger (defesa em profundidade)
CREATE FUNCTION financial.fn_block_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Tabela imutável: use a função de transição dedicada.';
END; $$;

CREATE TRIGGER trg_block_payments_mutation
    BEFORE UPDATE OR DELETE ON financial.payments
    FOR EACH ROW EXECUTE FUNCTION financial.fn_block_mutation();
-- (mesmo trigger para escrow_transactions, payouts, invoices, ledger_entries)

CREATE FUNCTION audit.fn_block_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'audit_logs é append-only.';
END; $$;

CREATE TRIGGER trg_block_audit_update
    BEFORE UPDATE ON audit.audit_logs
    FOR EACH ROW EXECUTE FUNCTION audit.fn_block_mutation();
CREATE TRIGGER trg_block_audit_delete
    BEFORE DELETE ON audit.audit_logs
    FOR EACH ROW EXECUTE FUNCTION audit.fn_block_mutation();

-- 4.3 Histórico de eventos de pagamento (transições imutáveis)
CREATE TABLE financial.payment_events (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid NOT NULL REFERENCES tenancy.tenants (id),
    payment_id  uuid NOT NULL REFERENCES financial.payments (id) ON DELETE RESTRICT,
    from_status smallint NOT NULL,
    to_status   smallint NOT NULL,
    actor_user_id uuid NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    reason      varchar(200)
);
CREATE INDEX ix_payment_events_payment ON financial.payment_events (payment_id);
CREATE INDEX ix_payment_events_tenant  ON financial.payment_events (tenant_id, occurred_at DESC);

-- 4.4 Transição controlada (única via de mudança de status)
CREATE FUNCTION financial.transition_payment(
    p_payment_id uuid, p_to_status smallint, p_actor uuid, p_reason varchar)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = financial, pg_temp
AS $$
DECLARE
    v_tenant uuid; v_from smallint;
BEGIN
    SELECT tenant_id, status INTO v_tenant, v_from
    FROM financial.payments WHERE id = p_payment_id;

    IF v_tenant IS NULL THEN
        RAISE EXCEPTION 'Pagamento inexistente';
    END IF;

    -- Máquina de estados: 1=Pending 2=Authorized 3=Captured 4=Failed 5=Refunded
    IF NOT ((v_from = 1 AND p_to_status IN (2, 4))
         OR (v_from = 2 AND p_to_status IN (3, 4, 5))
         OR (v_from = 3 AND p_to_status = 5)) THEN
        RAISE EXCEPTION 'Transição % -> % inválida', v_from, p_to_status;
    END IF;

    UPDATE financial.payments SET status = p_to_status, updated_at = now()
    WHERE id = p_payment_id;

    INSERT INTO financial.payment_events
        (tenant_id, payment_id, from_status, to_status, actor_user_id, reason)
    VALUES (v_tenant, p_payment_id, v_from, p_to_status, p_actor, p_reason);
END; $$;

-- 4.5 Ledger (append-only) — fonte da verdade financeira
CREATE TABLE financial.ledger_entries (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL REFERENCES tenancy.tenants (id),
    sequence       bigint NOT NULL,                -- por tenant, sem lacunas (função next)
    entry_type     smallint NOT NULL,              -- 1=Debit 2=Credit 3=Reversal
    amount         numeric(12,2) NOT NULL,
    currency       char(3) NOT NULL,
    reference_type varchar(50) NOT NULL,           -- 'Payment','Payout','Invoice'
    reference_id   uuid NOT NULL,
    created_by     uuid NOT NULL,
    created_at     timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_ledger_tenant_sequence UNIQUE (tenant_id, sequence)
);
CREATE INDEX ix_ledger_tenant_time ON financial.ledger_entries (tenant_id, created_at DESC);

-- 4.6 Auditoria com hash chain (detecção de adulteração)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

ALTER TABLE audit.audit_logs ADD COLUMN IF NOT EXISTS prev_hash text;
ALTER TABLE audit.audit_logs ADD COLUMN IF NOT EXISTS row_hash text;

CREATE FUNCTION audit.fn_chain() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    v_prev text;
BEGIN
    SELECT row_hash INTO v_prev
    FROM audit.audit_logs
    WHERE tenant_id IS NOT DISTINCT FROM NEW.tenant_id
    ORDER BY occurred_at DESC, id DESC
    LIMIT 1;

    NEW.row_hash := encode(digest(
        coalesce(v_prev, '')
        || NEW.occurred_at::text || NEW.action
        || coalesce(NEW.entity_type, '') || coalesce(NEW.entity_id::text, '')
        || coalesce(NEW.after::text, ''),
        'sha256'), 'hex');
    NEW.prev_hash := v_prev;
    RETURN NEW;
END; $$;

CREATE TRIGGER trg_audit_chain
    BEFORE INSERT ON audit.audit_logs
    FOR EACH ROW EXECUTE FUNCTION audit.fn_chain();

-- Verificação periódica (job diário):
-- SELECT * FROM audit.audit_logs WHERE row_hash != encode(digest(
--   coalesce(prev_hash,'') || occurred_at::text || action || ..., 'sha256'),'hex');
```

> **Importante:** `digest()` requer `pgcrypto`. Em ambiente sem a extensão,
> usar `sha256()` nativo do PostgreSQL 16 (`encode(sha256(...), 'hex')`).

## 5. Regras da aplicação

1. **Interceptor de auditoria** (mesmo `SaveChangesInterceptor` já previsto):
   grava `before`/`after` **na mesma transação** da operação — se a auditoria
   falha, a operação desfaz.
2. **Redação:** campos sensíveis (`bank_data`, `password_hash`, token de
   refresh) são substituídos por `"[REDACTED]"` no `before`/`after` — nunca
   auditados em claro.
3. **Nenhum endpoint** expõe UPDATE/DELETE de `payments`, `invoices`,
   `payouts`, `audit_logs`; alterações de status passam pelas funções
   dedicadas (ou pelo domínio, que chama as funções via repositório).
4. **Estorno:** valores errados são corrigidos com `entry_type = Reversal` +
   novo lançamento — nunca edição do registro original.
5. **Retenção:** `audit_logs` particionado por mês; drop de partições antigas
   executado por `worfair_migrator` (nunca pela app) e registrado em log.
6. **`worfair_migrator`** é a única role com `UPDATE/DELETE` nessas tabelas —
   acessível apenas em manutenção controlada (backup/restore, correção
   extraordinária com ADR).
7. **Read-only na API:** consulta de histórico exige `audit.read`
   (contratante) — e a leitura é sempre isolada por tenant (RLS).

## 6. Checklist de testes obrigatórios

- [ ] `UPDATE audit_logs`/`DELETE audit_logs` ⇒ exceção do trigger (app e SQL direto).
- [ ] `UPDATE payments`/`DELETE payments` ⇒ exceção do trigger.
- [ ] `transition_payment` rejeita transição inválida (ex.: Pending → Captured).
- [ ] Toda transição gera linha em `payment_events` (mesma transação).
- [ ] Estorno cria `ledger_entries` do tipo Reversal; saldo recalcula igual.
- [ ] `before`/`after` não contêm campos redigidos (`bank_data`, hashes).
- [ ] Auditoria da operação e a própria operação falham juntas (rollback).
- [ ] Job de verificação de hash chain não reporta divergências.
- [ ] Leitura de histórico de outro tenant retorna vazio (RLS).