import { useState } from 'react';
import { Link } from 'react-router-dom';
import { financialApi, messagesApi } from '../../api/endpoints/marketplace';
import { useAccess } from '../../access/useAccess';
import { useQuery, apiMessage } from '../../hooks/useQuery';
import { Skeleton, EmptyState, ErrorState } from '../../components/ui/Skeleton';
import { Field } from '../../components/ui/widgets';

// Conversa por fatura + encaminhar conflito ao administrador (disputa).
export default function MessagesPage() {
  const { me, isGlobalAdmin } = useAccess();
  const invoices = useQuery(() => financialApi.invoices());
  const [selected, setSelected] = useState(null);
  const [body, setBody] = useState('');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState(null);
  const thread = useQuery(() => (selected ? messagesApi.byInvoice(selected) : Promise.resolve({ data: [] })));

  const pick = async (id) => {
    setSelected(id);
    setFeedback(null);
  };

  const refreshThread = async () => {
    try {
      await thread.refetch();
    } catch {
      // thread carrega sob demanda
    }
  };

  const send = async (e) => {
    e.preventDefault();
    if (!selected) return;
    setBusy(true);
    setFeedback(null);
    try {
      const inv = (invoices.data ?? []).find((x) => x.id === selected);
      const recipient =
        inv?.clientUserId === me?.userId ? inv?.providerUserId : inv?.clientUserId;
      await messagesApi.send({ recipientUserId: recipient, invoiceId: selected, body });
      setBody('');
      await refreshThread();
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  const dispute = async (e) => {
    e.preventDefault();
    if (!selected) return;
    setBusy(true);
    setFeedback(null);
    try {
      await financialApi.openDispute({ invoiceId: selected, reason });
      setReason('');
      setFeedback({ ok: true, text: 'Problema reportado. O administrador vai mediar.' });
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <h2>Mensagens</h2>
      {isGlobalAdmin ? (
        <div className="alert alert-info">
          Você está no contexto global. As conversas pertencem às faturas dos espaços — para
          mediar, abra a <Link to="/plataforma">Plataforma</Link> e veja cada disputa com a
          conversa junto.
        </div>
      ) : (
      <>
      <p className="text-muted small">
        Converse com a outra parte da fatura. Conflitos são mediados exclusivamente pelo
        administrador da plataforma.
      </p>
      <div className="row g-3">
        <div className="col-md-4">
          <h5>Faturas</h5>
          {invoices.loading && <Skeleton lines={3} />}
          {invoices.error && <ErrorState message={apiMessage(invoices.error)} onRetry={() => invoices.refetch().catch(() => {})} />}
          <div className="list-group">
            {(invoices.data ?? []).map((inv) => (
              <button
                key={inv.id}
                type="button"
                className={`list-group-item list-group-item-action ${selected === inv.id ? 'active' : ''}`}
                onClick={() => pick(inv.id)}
              >
                <div className="small text-truncate">{inv.description}</div>
                <div className="small opacity-75"><code>{inv.id.slice(0, 8)}…</code> · {inv.status}</div>
              </button>
            ))}
          </div>
          {(invoices.data ?? []).length === 0 && !invoices.loading && <EmptyState title="Sem faturas" />}
        </div>
        <div className="col-md-8">
          {!selected && <EmptyState title="Escolha uma fatura" hint="A conversa é sempre vinculada a uma fatura." />}
          {selected && (
            <>
              <h5>Conversa da fatura</h5>
              <div className="thread border rounded p-2 mb-3 bg-white">
                {(thread.data ?? []).length === 0 && <div className="small text-muted">Sem mensagens ainda.</div>}
                {(thread.data ?? []).map((m) => (
                  <div key={m.id} className={`mb-2 ${m.senderUserId === me?.userId ? 'text-end' : ''}`}>
                    <span className={`badge ${m.senderUserId === me?.userId ? 'bg-primary' : 'bg-secondary'}`}>
                      {m.body}
                    </span>
                  </div>
                ))}
              </div>
              <form onSubmit={send} className="d-flex gap-2 mb-4">
                <input
                  className="form-control" required value={body}
                  onChange={(e) => setBody(e.target.value)} placeholder="Escrever mensagem…"
                />
                <button className="btn btn-primary" disabled={busy} type="submit">Enviar</button>
              </form>
              <div className="card">
                <div className="card-body">
                  <h6>Reportar problema ao administrador</h6>
                  <p className="small text-muted">
                    A mediação é feita pelo super admin, que verá esta fatura e a conversa.
                  </p>
                  <form onSubmit={dispute}>
                    <Field label="Motivo">
                      <textarea className="form-control" rows={2} required value={reason} onChange={(e) => setReason(e.target.value)} />
                    </Field>
                    <button className="btn btn-sm btn-outline-danger" disabled={busy} type="submit">
                      Reportar (mediação do super admin)
                    </button>
                  </form>
                </div>
              </div>
            </>
          )}
          {feedback && (
            <div className={`alert mt-3 ${feedback.ok ? 'alert-success' : 'alert-danger'}`} role="alert">
              {feedback.text}
            </div>
          )}
        </div>
      </div>
      </>
      )}
    </div>
  );
}
