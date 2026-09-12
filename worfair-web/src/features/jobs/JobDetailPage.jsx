import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { jobsApi, proposalsApi } from '../../api/endpoints/marketplace';
import { useAuth } from '../../auth/useAuth';
import { useAccess } from '../../access/useAccess';
import { useQuery, apiMessage } from '../../hooks/useQuery';
import { Skeleton, ErrorState } from '../../components/ui/Skeleton';
import { Field } from '../../components/ui/widgets';
import { money } from '../../utils/money';

const REMOTE = { 0: 'Presencial', 1: 'Híbrido', 2: 'Remoto' };

// Detalhe de vaga (kind=job) ou trabalho (kind=project) com candidatura/proposta.
export default function JobDetailPage({ kind = 'job' }) {
  const { id } = useParams();
  const { isAuthenticated } = useAuth();
  const { can } = useAccess();
  const isJob = kind === 'job';
  const q = useQuery(() => (isJob ? jobsApi.getPosting(id) : jobsApi.getProject(id)));

  const [message, setMessage] = useState('');
  const [amount, setAmount] = useState('');
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState(null);

  const canApply = can('jobs.project.apply');
  const canPropose = can('proposals.submit');

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true);
    setFeedback(null);
    try {
      if (isJob) {
        await jobsApi.applyToJob(id, { message });
        setFeedback({ ok: true, text: 'Candidatura enviada!' });
      } else {
        await proposalsApi.submit({
          serviceProjectId: id,
          jobPostingId: null,
          providerCompanyId: null,
          message,
          amount: Number(amount),
          currency: 'BRL',
        });
        setFeedback({ ok: true, text: 'Proposta enviada! Acompanhe em Propostas.' });
      }
      setMessage('');
      setAmount('');
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  const item = q.data;

  return (
    <div>
      <Link to={isJob ? '/vagas' : '/trabalhos'} className="small">← voltar</Link>
      {q.loading && <Skeleton lines={4} />}
      {q.error && <ErrorState message={apiMessage(q.error)} onRetry={() => q.refetch().catch(() => {})} />}
      {item && (
        <>
          <h2 className="mt-2">{item.title}</h2>
          <p className="mb-2">
            {item.companyName && <><strong>{item.companyName}</strong> · </>}
            {item.category && <span className="badge bg-secondary">{item.category}</span>}
          </p>
          {isJob
            ? <p className="text-muted">{item.location ?? 'Local a combinar'} · {REMOTE[item.remote] ?? '—'}</p>
            : (
              <p>
                Orçamento: <strong>{money(item.budgetMin, item.currency)}</strong>
                {item.budgetMax ? <> até <strong>{money(item.budgetMax, item.currency)}</strong></> : null}
              </p>
            )}
          <p style={{ whiteSpace: 'pre-wrap' }}>{item.description}</p>

          <div className="card mt-4">
            <div className="card-body">
              <h5>{isJob ? 'Candidatar-se' : 'Enviar proposta'}</h5>
              {!isAuthenticated ? (
                <div className="alert alert-info mb-0">
                  <Link to="/login">Entre</Link> ou <Link to="/cadastro">crie sua conta</Link>{' '}
                  para {isJob ? 'se candidatar a esta vaga' : 'enviar proposta neste trabalho'}.
                </div>
              ) : (
              <>
              {isJob && !canApply && (
                <div className="alert alert-warning small">Seu contexto atual não tem permissão de candidatura.</div>
              )}
              {!isJob && !canPropose && (
                <div className="alert alert-warning small">Seu contexto atual não tem permissão para propor.</div>
              )}
              <form onSubmit={submit}>
                {!isJob && (
                  <Field label="Valor da sua proposta (R$) — é o que você recebe">
                    <input
                      className="form-control" type="number" min="1" step="0.01" required
                      value={amount} onChange={(e) => setAmount(e.target.value)}
                    />
                  </Field>
                )}
                {!isJob && Number(amount) > 0 && (
                  <div className="alert alert-success small" role="note">
                    Pelo trabalho de {money(Number(amount))}, você recebe{' '}
                    <strong>{money(Number(amount))}</strong>.
                  </div>
                )}
                <Field label={isJob ? 'Mensagem de candidatura' : 'Mensagem da proposta'}>
                  <textarea
                    className="form-control" rows={4} required
                    value={message} onChange={(e) => setMessage(e.target.value)}
                  />
                </Field>
                {feedback && (
                  <div className={`alert ${feedback.ok ? 'alert-success' : 'alert-danger'}`} role="alert">
                    {feedback.text}
                  </div>
                )}
                <button
                  className="btn btn-primary" type="submit" disabled={busy || (isJob ? !canApply : !canPropose)}
                >
                  {busy ? 'Enviando…' : isJob ? 'Candidatar-se' : 'Enviar proposta'}
                </button>
              </form>
              </>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
