import { useState } from 'react';
import { financialApi, messagesApi, tenantsApi } from '../../api/endpoints/marketplace';
import { useAccess } from '../../access/useAccess';
import { useQuery, apiMessage } from '../../hooks/useQuery';
import { Skeleton, ErrorState } from '../../components/ui/Skeleton';
import { RequirePermission } from '../../routing/RequirePermission';
import { StatusBadge } from '../../components/ui/widgets';
import { Field } from '../../components/ui/widgets';
import { money } from '../../utils/money';

// Painel do SUPER_ADMIN: provisionar tenants e mediar disputas.
export default function PlatformPage() {
  return (
    <RequirePermission permission="platform.tenants.manage">
      <PlatformContent />
    </RequirePermission>
  );
}

function PlatformContent() {
  const { can, me } = useAccess();
  const tenants = useQuery(() => tenantsApi.listPlatform());
  const disputes = useQuery(() => financialApi.disputes().catch(() => ({ data: [] })));
  const revenue = useQuery(() => financialApi.revenue().catch(() => ({ data: null })));
  const [form, setForm] = useState({ name: '', slug: '' });
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState(null);
  const [expanded, setExpanded] = useState(null);
  const [context, setContext] = useState({});
  const [reply, setReply] = useState({});
  const [replyTo, setReplyTo] = useState({});

  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));

  const provision = async (e) => {
    e.preventDefault();
    setBusy(true);
    setFeedback(null);
    try {
      await tenantsApi.provision({ name: form.name, slug: form.slug, tier: 1, timezone: null, locale: null, ownerUserId: null });
      setFeedback({ ok: true, text: 'Tenant provisionado!' });
      setForm({ name: '', slug: '' });
      await tenants.refetch();
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  const resolve = async (id, accepted) => {
    setBusy(true);
    try {
      await financialApi.resolveDispute(id, accepted);
      await disputes.refetch();
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  // Fala do mediador na fatura em disputa (vai para a conversa das partes).
  const sendReply = async (d) => {
    const body = (reply[d.id] ?? '').trim();
    const recipient = replyTo[d.id];
    if (!body || !recipient) return;
    setBusy(true);
    try {
      await messagesApi.send({ recipientUserId: recipient, invoiceId: d.invoiceId, body });
      setReply((r) => ({ ...r, [d.id]: '' }));
      const thread = await messagesApi.byInvoice(d.invoiceId).catch(() => ({ data: [] }));
      setContext((c) => ({ ...c, [d.id]: { ...c[d.id], thread: thread.data ?? [] } }));
      setFeedback({ ok: true, text: 'Mensagem enviada às partes.' });
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };
  const toggleContext = async (d) => {
    if (expanded === d.id) {
      setExpanded(null);
      return;
    }
    setExpanded(d.id);
    if (context[d.id]) return;
    try {
      const [inv, thread] = await Promise.all([
        financialApi.invoiceDetail(d.invoiceId).catch(() => ({ data: null })),
        messagesApi.byInvoice(d.invoiceId).catch(() => ({ data: [] })),
      ]);
      setContext((c) => ({ ...c, [d.id]: { invoice: inv.data, thread: thread.data ?? [] } }));
    } catch {
      setContext((c) => ({ ...c, [d.id]: { invoice: null, thread: [] } }));
    }
  };

  return (
    <div>
      <h2>Plataforma (super admin)</h2>

      {revenue.data && (
        <div className="row g-3 mb-4">
          <div className="col-md-4">
            <div className="card"><div className="card-body">
              <h6 className="text-muted">Taxas recebidas</h6>
              <p className="fs-5 mb-0">{money(revenue.data.feesReceived, revenue.data.currency)}</p>
            </div></div>
          </div>
          <div className="col-md-4">
            <div className="card"><div className="card-body">
              <h6 className="text-muted">Taxas a receber</h6>
              <p className="fs-5 mb-0">{money(revenue.data.feesPending, revenue.data.currency)}</p>
            </div></div>
          </div>
          <div className="col-md-4">
            <div className="card"><div className="card-body">
              <h6 className="text-muted">Relatório</h6>
              <p className="mb-0 small">
                {revenue.data.paidCount} pagas · {revenue.data.issuedCount} abertas ·{' '}
                {revenue.data.invoicesCount} faturas · volume {money(revenue.data.volumePaid, revenue.data.currency)}
              </p>
            </div></div>
          </div>
        </div>
      )}

      <div className="card mb-4">
        <div className="card-body">
          <h5>Provisionar tenant</h5>
          <form onSubmit={provision}>
            <div className="row">
              <div className="col-md-6">
                <Field label="Nome">
                  <input className="form-control" required value={form.name} onChange={set('name')} />
                </Field>
              </div>
              <div className="col-md-6">
                <Field label="Slug" hint="minúsculas, números e hífens.">
                  <input className="form-control" required value={form.slug} onChange={set('slug')} placeholder="empresa-x" />
                </Field>
              </div>
            </div>
            {feedback && (
              <div className={`alert ${feedback.ok ? 'alert-success' : 'alert-danger'}`} role="alert">{feedback.text}</div>
            )}
            <button className="btn btn-dark" type="submit" disabled={busy}>Provisionar</button>
          </form>
        </div>
      </div>

      <h5>Tenants ativos ({(tenants.data ?? []).length})</h5>
      {tenants.loading && <Skeleton lines={2} />}
      {tenants.error && <ErrorState message={apiMessage(tenants.error)} onRetry={() => tenants.refetch().catch(() => {})} />}
      <div className="list-group mb-4">
        {(tenants.data ?? []).map((t) => (
          <div key={t.id} className="list-group-item">
            <strong>{t.name}</strong> <code>{t.slug}</code>
            <div className="small text-muted">{t.id}</div>
          </div>
        ))}
      </div>

      <h5>Disputas para mediação ({(disputes.data ?? []).length})</h5>
      <div className="list-group">
        {(disputes.data ?? []).map((d) => (
          <div key={d.id} className="list-group-item">
            <div className="d-flex justify-content-between align-items-center">
              <code className="small">{d.id}</code>
              <StatusBadge value={d.status} />
            </div>
            <div className="small">Fatura <code>{d.invoiceId}</code></div>
            <div className="small">{d.reason}</div>
            <button
              type="button" className="btn btn-sm btn-outline-secondary mt-2"
              onClick={() => toggleContext(d)}
            >
              {expanded === d.id ? 'Ocultar contexto' : 'Ver fatura + conversa'}
            </button>
            {expanded === d.id && (
              <div className="border rounded p-2 mt-2 bg-light">
                {context[d.id]?.invoice ? (
                  <div className="small mb-2">
                    Fatura: <strong>{context[d.id].invoice.totalAmount} {context[d.id].invoice.currency}</strong>
                    {' '}· {context[d.id].invoice.description}
                    {' '}· status {context[d.id].invoice.status}
                  </div>
                ) : (
                  <div className="small text-muted">Fatura indisponível.</div>
                )}
                <div className="small fw-semibold">Conversa:</div>
                {(context[d.id]?.thread ?? []).length === 0 && (
                  <div className="small text-muted">Sem mensagens.</div>
                )}
                {(context[d.id]?.thread ?? []).map((m) => (
                  <div key={m.id} className="small border-bottom py-1">{m.body}</div>
                ))}
                <div className="mt-2">
                  <div className="small fw-semibold">Responder como mediador:</div>
                  <div className="d-flex gap-2 mt-1">
                    <select
                      className="form-select form-select-sm" style={{ maxWidth: 220 }}
                      aria-label="Destinatário"
                      value={replyTo[d.id] ?? ''}
                      onChange={(e) => setReplyTo((s) => ({ ...s, [d.id]: e.target.value }))}
                    >
                      <option value="">Para…</option>
                      {[context[d.id]?.invoice?.clientUserId, context[d.id]?.invoice?.providerUserId, d.openedByUserId]
                        .filter((v, i, a) => v && a.indexOf(v) === i)
                        .map((u) => (
                          <option key={u} value={u}>{u === d.openedByUserId ? `Autor (${u.slice(0, 8)}…)` : `${u.slice(0, 8)}…`}</option>
                        ))}
                    </select>
                    <input
                      className="form-control form-control-sm" placeholder="Mensagem do mediador…"
                      value={reply[d.id] ?? ''}
                      onChange={(e) => setReply((r) => ({ ...r, [d.id]: e.target.value }))}
                    />
                    <button
                      type="button" className="btn btn-sm btn-primary"
                      disabled={busy || !(reply[d.id] ?? '').trim() || !replyTo[d.id]}
                      onClick={() => sendReply(d)}
                    >
                      Enviar
                    </button>
                  </div>
                </div>
              </div>
            )}
            {can('financial.dispute.mediate') !== false && (d.status === 'Open' || d.status === 'UnderMediation') && (
              <div className="mt-2 d-flex gap-2">
                <button className="btn btn-sm btn-success" disabled={busy} onClick={() => resolve(d.id, true)}>
                  Resolver a favor
                </button>
                <button className="btn btn-sm btn-outline-danger" disabled={busy} onClick={() => resolve(d.id, false)}>
                  Rejeitar
                </button>
              </div>
            )}
          </div>
        ))}
        {(disputes.data ?? []).length === 0 && <div className="small text-muted">Nenhuma disputa.</div>}
      </div>
    </div>
  );
}
