import { useState } from 'react';
import { tenantsApi } from '../../api/endpoints/marketplace';
import { useQuery, apiMessage } from '../../hooks/useQuery';
import { Skeleton, EmptyState, ErrorState } from '../../components/ui/Skeleton';

const ROLES = ['OWNER', 'CLIENT', 'RECRUITER', 'HIRING_MANAGER', 'PROVIDER'];

// Equipe do espaço: convidar por e-mail + atribuir/remover cargos.
export default function TeamPage() {
  const q = useQuery(() => tenantsApi.members());
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState(null);
  const [roleSel, setRoleSel] = useState({});

  const invite = async (e) => {
    e.preventDefault();
    setBusy(true);
    setFeedback(null);
    try {
      const { data } = await tenantsApi.addMemberByEmail(email.trim());
      setFeedback({ ok: true, text: `Membro adicionado: ${data.email ?? data.userId}` });
      setEmail('');
      await q.refetch();
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  const assign = async (userId) => {
    const role = roleSel[userId];
    if (!role) return;
    setBusy(true);
    setFeedback(null);
    try {
      await tenantsApi.assignRole(userId, role);
      setFeedback({ ok: true, text: `Cargo ${role} atribuído.` });
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  const remove = async (userId) => {
    const role = roleSel[userId];
    if (!role) return;
    setBusy(true);
    setFeedback(null);
    try {
      await tenantsApi.removeRole(userId, role);
      setFeedback({ ok: true, text: `Cargo ${role} removido.` });
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <h2>Equipe e cargos</h2>
      <p className="text-muted">
        Convide pelo e-mail da conta e distribua cargos: dono, contratante, recrutador, gestor e
        prestador. Cargos valem dentro deste espaço.
      </p>

      <div className="card mb-4">
        <div className="card-body">
          <h5>Convidar por e-mail</h5>
          <form onSubmit={invite} className="d-flex gap-2">
            <input
              className="form-control" type="email" required value={email}
              onChange={(e) => setEmail(e.target.value)} placeholder="pessoa@exemplo.com"
            />
            <button className="btn btn-primary" type="submit" disabled={busy}>Convidar</button>
          </form>
        </div>
      </div>

      {feedback && (
        <div className={`alert ${feedback.ok ? 'alert-success' : 'alert-danger'}`} role="alert">
          {feedback.text}
        </div>
      )}

      <h5>Membros ({(q.data ?? []).length})</h5>
      {q.loading && <Skeleton lines={3} />}
      {q.error && <ErrorState message={apiMessage(q.error)} onRetry={() => q.refetch().catch(() => {})} />}
      {!q.loading && !q.error && (q.data ?? []).length === 0 && <EmptyState title="Sem membros" />}
      <div className="list-group">
        {(q.data ?? []).map((m) => (
          <div key={m.userId} className="list-group-item">
            <code className="small">{m.userId}</code>
            <div className="d-flex gap-2 mt-2">
              <select
                className="form-select form-select-sm"
                aria-label="Cargo"
                value={roleSel[m.userId] ?? ''}
                onChange={(e) => setRoleSel((s) => ({ ...s, [m.userId]: e.target.value }))}
              >
                <option value="">Cargo…</option>
                {ROLES.map((r) => (
                  <option key={r} value={r}>{r}</option>
                ))}
              </select>
              <button className="btn btn-sm btn-outline-primary" disabled={busy || !roleSel[m.userId]} onClick={() => assign(m.userId)}>
                Atribuir
              </button>
              <button className="btn btn-sm btn-outline-danger" disabled={busy || !roleSel[m.userId]} onClick={() => remove(m.userId)}>
                Remover
              </button>
            </div>
          </div>
        ))}
      </div>

      <p className="small text-muted mt-3">
        Para vincular uma empresa a um responsável, use a página Empresa.
      </p>
    </div>
  );
}
