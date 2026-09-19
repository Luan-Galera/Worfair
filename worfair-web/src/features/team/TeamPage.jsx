import { useState } from 'react';
import { tenantsApi } from '../../api/endpoints/marketplace';
import { useAccess } from '../../access/useAccess';
import { useQuery, apiMessage } from '../../hooks/useQuery';
import { Skeleton, EmptyState, ErrorState } from '../../components/ui/Skeleton';

const ROLES = [
  { code: 'OWNER', label: 'Dono' },
  { code: 'CLIENT', label: 'Contratante' },
  { code: 'RECRUITER', label: 'Recrutador' },
  { code: 'HIRING_MANAGER', label: 'Gestor' },
  { code: 'PROVIDER', label: 'Prestador' },
];

const roleLabel = (code) => ROLES.find((r) => r.code === code)?.label ?? code;

// Equipe do espaço: convidar por e-mail + atribuir/remover cargos.
export default function TeamPage() {
  const { me } = useAccess();
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
      setFeedback({ ok: true, text: `Cargo ${roleLabel(role)} atribuído.` });
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  const transfer = async (member) => {
    const name = member.fullName || member.email || 'este membro';
    if (!window.confirm(`Transferir a propriedade do espaço para ${name}? Você deixará de ser dono.`)) return;
    setBusy(true);
    setFeedback(null);
    try {
      await tenantsApi.transferOwnership(member.userId);
      setFeedback({ ok: true, text: `Propriedade transferida para ${name}.` });
      await q.refetch();
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
      setFeedback({ ok: true, text: `Cargo ${roleLabel(role)} removido.` });
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
            <strong>{m.fullName || m.email || 'Membro'}</strong>
            {m.email && m.fullName && <div className="small text-muted">{m.email}</div>}
            {(m.roleCodes ?? []).length > 0 && (
              <div className="mt-1 d-flex flex-wrap gap-1">
                {(m.roleCodes ?? []).map((c) => (
                  <span key={c} className="badge bg-secondary">{roleLabel(c)}</span>
                ))}
              </div>
            )}
            <div className="d-flex gap-2 mt-2">
              <select
                className="form-select form-select-sm"
                aria-label="Cargo"
                value={roleSel[m.userId] ?? ''}
                onChange={(e) => setRoleSel((s) => ({ ...s, [m.userId]: e.target.value }))}
              >
                <option value="">Cargo…</option>
                {ROLES.map((r) => (
                  <option key={r.code} value={r.code}>{r.label}</option>
                ))}
              </select>
              <button className="btn btn-sm btn-outline-primary" disabled={busy || !roleSel[m.userId]} onClick={() => assign(m.userId)}>
                Atribuir
              </button>
              <button className="btn btn-sm btn-outline-danger" disabled={busy || !roleSel[m.userId]} onClick={() => remove(m.userId)}>
                Remover
              </button>
              {me?.userId && me.userId !== m.userId && (
                <button
                  className="btn btn-sm btn-outline-warning"
                  title="Passa a propriedade do espaço para este membro (você deixa de ser dono)"
                  disabled={busy}
                  onClick={() => transfer(m)}
                >
                  Transferir propriedade
                </button>
              )}
            </div>
          </div>
        ))}
      </div>

      <p className="small text-muted mt-3">
        Para vincular uma empresa a um responsável, use a página Empresa. Para passar a
        propriedade do espaço a outro membro, use “Transferir propriedade” (você deixa de ser dono).
      </p>
    </div>
  );
}
