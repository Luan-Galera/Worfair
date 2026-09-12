import { Link } from 'react-router-dom';
import { notificationsApi } from '../../api/endpoints/marketplace';
import { useAccess } from '../../access/useAccess';
import { useQuery, apiMessage } from '../../hooks/useQuery';
import { Skeleton, EmptyState, ErrorState } from '../../components/ui/Skeleton';

export default function NotificationsPage() {
  const { isGlobalAdmin } = useAccess();
  const q = useQuery(() => notificationsApi.list());

  // Notificações pertencem aos usuários dentro dos espaços; no contexto
  // global o admin acompanha as disputas pela Plataforma.
  if (isGlobalAdmin) {
    return (
      <div>
        <h2>Notificações</h2>
        <div className="alert alert-info">
          No contexto global não há notificações pessoais. Novas disputas para mediação estão na{' '}
          <Link to="/plataforma">Plataforma</Link>.
        </div>
      </div>
    );
  }

  const mark = async (id) => {
    try {
      await notificationsApi.markRead(id);
      await q.refetch();
    } catch {
      // best-effort
    }
  };

  return (
    <div>
      <h2>Notificações</h2>
      {q.loading && <Skeleton lines={4} />}
      {q.error && <ErrorState message={apiMessage(q.error)} onRetry={() => q.refetch().catch(() => {})} />}
      {!q.loading && !q.error && (q.data ?? []).length === 0 && <EmptyState icon="bi-bell" title="Sem notificações" />}
      <div className="list-group">
        {(q.data ?? []).map((n) => (
          <div key={n.id} className={`list-group-item ${n.isRead ? '' : 'list-group-item-warning'}`}>
            <div className="d-flex justify-content-between align-items-center">
              <strong>{n.title}</strong>
              {!n.isRead && (
                <button className="btn btn-sm btn-outline-secondary" onClick={() => mark(n.id)}>
                  Marcar lida
                </button>
              )}
            </div>
            <div className="small">{n.message}</div>
            <div className="small text-muted">{n.category} · {n.priority}</div>
          </div>
        ))}
      </div>
    </div>
  );
}
