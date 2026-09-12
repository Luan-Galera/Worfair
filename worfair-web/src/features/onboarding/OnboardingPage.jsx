import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../../api/endpoints/auth';
import { useAuth } from '../../auth/useAuth';
import { useAccess } from '../../access/useAccess';
import { Field } from '../../components/ui/widgets';
import { apiMessage } from '../../hooks/useQuery';

// Onboarding: contas novas entram sem contexto (ModeUnavailable) até ganharem
// membership. Esta página cria o espaço pessoal (OWNER + PROVIDER) e entra nele.
export default function OnboardingPage() {
  const { switchTenant } = useAuth();
  const { memberships, refreshMe } = useAccess();
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const [created, setCreated] = useState(null);

  const create = async (e) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const { data } = await authApi.bootstrap(name.trim() || undefined);
      setCreated(data);
      await switchTenant(data.tenantId);
      await refreshMe();
      navigate('/painel', { replace: true });
    } catch (err) {
      if (err?.response?.status === 409) {
        await refreshMe();
        navigate('/painel', { replace: true });
        return;
      }
      setError(apiMessage(err, 'Não foi possível criar seu espaço.'));
    } finally {
      setBusy(false);
    }
  };

  const enter = async (tenantId) => {
    setBusy(true);
    setError(null);
    try {
      await switchTenant(tenantId);
      await refreshMe();
      navigate('/painel', { replace: true });
    } catch (err) {
      setError(apiMessage(err, 'Não foi possível entrar no espaço.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <h2>Bem-vindo! Crie seu espaço</h2>
      <p className="text-muted">
        Contas novas começam sem contexto — por isso o login pode retornar{' '}
        <code>ModeUnavailable</code>. Crie seu espaço pessoal para atuar como contratante e
        prestador, ou entre em um espaço existente.
      </p>

      {memberships.length > 0 && (
        <div className="card mb-3">
          <div className="card-body">
            <h5>Seus espaços</h5>
            <div className="list-group">
              {memberships.map((m) => (
                <button
                  key={m.tenantId}
                  type="button"
                  className="list-group-item list-group-item-action"
                  disabled={busy}
                  onClick={() => enter(m.tenantId)}
                >
                  {m.tenantName}
                </button>
              ))}
            </div>
          </div>
        </div>
      )}

      <div className="card">
        <div className="card-body">
          <h5>Criar espaço pessoal</h5>
          <form onSubmit={create}>
            <Field label="Nome do espaço" hint="Ex.: nome da sua empresa ou do seu estúdio.">
              <input
                className="form-control"
                value={name}
                maxLength={150}
                onChange={(e) => setName(e.target.value)}
                placeholder="Meu espaço"
              />
            </Field>
            {error && <div className="alert alert-danger" role="alert">{error}</div>}
            {created && <div className="alert alert-success">Espaço criado! Entrando…</div>}
            <button className="btn btn-primary" type="submit" disabled={busy}>
              {busy ? 'Criando…' : 'Criar e entrar'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
