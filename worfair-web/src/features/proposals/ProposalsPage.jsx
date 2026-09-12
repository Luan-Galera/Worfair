import { useState } from 'react';
import { Link } from 'react-router-dom';
import { proposalsApi, jobsApi } from '../../api/endpoints/marketplace';
import { useAccess } from '../../access/useAccess';
import { useQuery, apiMessage } from '../../hooks/useQuery';
import { Skeleton, EmptyState, ErrorState } from '../../components/ui/Skeleton';
import { StatusBadge, MoneyText } from '../../components/ui/widgets';

const PROPOSAL_STATUS = { 1: 'Enviada', 2: 'Aceita', 3: 'Recusada' };

// Enviar (prestador), acompanhar e aceitar/recusar (contratante).
export default function ProposalsPage() {
  const { can, me } = useAccess();
  const mine = useQuery(() => proposalsApi.mine().catch(() => ({ data: [] })));
  const inbox = useQuery(() => proposalsApi.list().catch(() => ({ data: [] })));
  const apps = useQuery(() => jobsApi.myApplications().catch(() => ({ data: [] })));
  const [busyId, setBusyId] = useState(null);
  const [error, setError] = useState(null);

  const decide = async (id, accept) => {
    setBusyId(id);
    setError(null);
    try {
      await proposalsApi.decide(id, accept);
      await inbox.refetch();
    } catch (err) {
      setError(apiMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  const canDecide = can('proposals.decide');

  return (
    <div>
      <h2>Propostas</h2>
      {mine.loading && <Skeleton lines={3} />}

      <h5 className="mt-3">Minhas propostas enviadas</h5>
      {(mine.data ?? []).length === 0 && !mine.loading && <EmptyState title="Nenhuma proposta enviada" />}
      <div className="list-group mb-4">
        {(mine.data ?? []).map((p) => (
          <div key={p.id} className="list-group-item">
            <div className="d-flex justify-content-between align-items-center">
              <strong><MoneyText value={p.amount} currency={p.currency} /></strong>
              <StatusBadge value={p.status} label={PROPOSAL_STATUS[p.status] ?? p.status} />
            </div>
            <div className="small text-muted">Você recebe este valor pelo trabalho.</div>
            <div className="small">{p.message}</div>
          </div>
        ))}
      </div>

      <h5>Minhas candidaturas a vagas</h5>
      <div className="list-group mb-4">
        {(apps.data ?? []).map((a) => (
          <div key={a.id} className="list-group-item">
            <div className="small">{a.message}</div>
            <StatusBadge value={a.status} label={`Status ${a.status}`} />
          </div>
        ))}
        {(apps.data ?? []).length === 0 && <div className="small text-muted">Nenhuma candidatura.</div>}
      </div>

      <h5>Caixa de entrada (decisão do contratante)</h5>
      {!canDecide && <div className="alert alert-info small">Seu contexto atual não decide propostas.</div>}
      {canDecide && (
        <div className="alert alert-warning small">
          Você é o contratante aqui: sobre o valor da proposta há a taxa de 15% — o total que
          você paga aparece em cada proposta.
        </div>
      )}
      {inbox.error && <ErrorState message={apiMessage(inbox.error)} onRetry={() => inbox.refetch().catch(() => {})} />}
      {error && <div className="alert alert-danger" role="alert">{error}</div>}
      <div className="list-group">
        {(inbox.data ?? [])
          .filter((p) => p.providerUserId !== me?.userId)
          .map((p) => (
            <div key={p.id} className="list-group-item">
              <div className="d-flex justify-content-between align-items-center">
                <strong><MoneyText value={p.amount} currency={p.currency} /></strong>
                <StatusBadge value={p.status} label={PROPOSAL_STATUS[p.status] ?? p.status} />
              </div>
              <div className="small">{p.message}</div>
              {p.status === 1 && canDecide && (
                <div className="mt-2 d-flex gap-2">
                  <button className="btn btn-sm btn-success" disabled={busyId === p.id} onClick={() => decide(p.id, true)}>
                    Aceitar
                  </button>
                  <button className="btn btn-sm btn-outline-danger" disabled={busyId === p.id} onClick={() => decide(p.id, false)}>
                    Recusar
                  </button>
                </div>
              )}
            </div>
          ))}
      </div>
    </div>
  );
}
