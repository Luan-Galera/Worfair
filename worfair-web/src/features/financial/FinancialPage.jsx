import { useState } from 'react';
import { financialApi, tenantsApi } from '../../api/endpoints/marketplace';
import { useQuery, apiMessage } from '../../hooks/useQuery';
import { Skeleton, EmptyState, ErrorState } from '../../components/ui/Skeleton';
import { Field, MoneyText, StatusBadge } from '../../components/ui/widgets';
import { money } from '../../utils/money';

// Gestão de faturas (super admin): a plataforma centraliza o dinheiro.
// Lista todas, cria entre usuários de um espaço e liquida.
export default function FinancialPage() {
  const invoices = useQuery(() => financialApi.adminInvoices());
  const tenants = useQuery(() => tenantsApi.listPlatform().catch(() => ({ data: [] })));
  const [form, setForm] = useState({
    tenantId: '', clientUserId: '', providerUserId: '', amount: '', description: '',
  });
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState(null);

  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));

  const create = async (e) => {
    e.preventDefault();
    setBusy(true);
    setFeedback(null);
    try {
      await financialApi.adminCreateInvoice({
        tenantId: form.tenantId,
        clientUserId: form.clientUserId,
        providerUserId: form.providerUserId,
        providerCompanyId: null,
        amount: Number(form.amount),
        currency: 'BRL',
        description: form.description,
      });
      setFeedback({ ok: true, text: 'Fatura criada!' });
      setForm({ tenantId: '', clientUserId: '', providerUserId: '', amount: '', description: '' });
      await invoices.refetch();
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  const settle = async (id) => {
    setBusy(true);
    try {
      await financialApi.adminSettle(id);
      await invoices.refetch();
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <h2>Faturas (super admin)</h2>
      <p className="text-muted">
        Todo o dinheiro passa pela plataforma: o contratante paga o valor + 15% de taxa, o
        prestador recebe o valor cheio e a taxa fica na plataforma.
      </p>

      <div className="card mb-4">
        <div className="card-body">
          <h5>Criar fatura</h5>
          <form onSubmit={create}>
            <Field label="Espaço">
              <select className="form-select" required value={form.tenantId} onChange={set('tenantId')}>
                <option value="">Selecionar…</option>
                {(tenants.data ?? []).map((t) => (
                  <option key={t.id} value={t.id}>{t.name}</option>
                ))}
              </select>
            </Field>
            <div className="row">
              <div className="col-md-6">
                <Field label="ID do contratante (usuário)">
                  <input className="form-control" required value={form.clientUserId} onChange={set('clientUserId')} placeholder="GUID" />
                </Field>
              </div>
              <div className="col-md-6">
                <Field label="ID do prestador (usuário)">
                  <input className="form-control" required value={form.providerUserId} onChange={set('providerUserId')} placeholder="GUID" />
                </Field>
              </div>
            </div>
            <div className="row">
              <div className="col-md-4">
                <Field label="Valor (R$, prestador recebe)">
                  <input className="form-control" type="number" min="1" step="0.01" required value={form.amount} onChange={set('amount')} />
                </Field>
              </div>
              <div className="col-md-8">
                <Field label="Descrição">
                  <input className="form-control" required value={form.description} onChange={set('description')} />
                </Field>
              </div>
            </div>
            {Number(form.amount) > 0 && (
              <div className="alert alert-warning small">
                Contratante pagará <strong>{money(Number(form.amount) * 1.15)}</strong> (taxa{' '}
                {money(Number(form.amount) * 0.15)}).
              </div>
            )}
            {feedback && (
              <div className={`alert ${feedback.ok ? 'alert-success' : 'alert-danger'}`} role="alert">{feedback.text}</div>
            )}
            <button className="btn btn-primary" type="submit" disabled={busy}>Criar fatura</button>
          </form>
        </div>
      </div>

      <h5>Todas as faturas ({(invoices.data ?? []).length})</h5>
      {invoices.loading && <Skeleton lines={3} />}
      {invoices.error && <ErrorState message={apiMessage(invoices.error)} onRetry={() => invoices.refetch().catch(() => {})} />}
      {(invoices.data ?? []).length === 0 && !invoices.loading && <EmptyState title="Sem faturas" />}
      <div className="list-group">
        {(invoices.data ?? []).map((inv) => (
          <div key={inv.id} className="list-group-item">
            <div className="d-flex justify-content-between align-items-center">
              <strong><MoneyText value={inv.totalAmount} currency={inv.currency} /></strong>
              <StatusBadge value={inv.status} />
            </div>
            <div className="small text-muted">
              Valor <MoneyText value={inv.amount} currency={inv.currency} />
              {' '}· taxa <MoneyText value={inv.platformFeeAmount} currency={inv.currency} />
            </div>
            <div className="small">{inv.description}</div>
            <div className="small text-muted"><code>{inv.id}</code></div>
            {inv.status === 'Issued' && (
              <button className="btn btn-sm btn-outline-success mt-2" disabled={busy} onClick={() => settle(inv.id)}>
                Liquidar
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
