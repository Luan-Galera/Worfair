import { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/useAuth';
import { useAccess } from '../../access/useAccess';
import { Field } from '../../components/ui/widgets';
import { apiMessage } from '../../hooks/useQuery';

export default function LoginPage() {
  const { login } = useAuth();
  const { refreshMe } = useAccess();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async (e) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(email.trim(), password);
      const me = await refreshMe();
      const from = location.state?.from;
      if (from && from !== '/login') {
        navigate(from, { replace: true });
      } else if (!me?.tenantId && !(me?.roles ?? []).includes('SUPER_ADMIN')) {
        navigate('/onboarding', { replace: true });
      } else {
        navigate('/painel', { replace: true });
      }
    } catch (err) {
      setError(apiMessage(err, 'Login falhou. Confira e-mail e senha.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="row justify-content-center">
      <div className="col-12 col-md-6">
        <h2>Entrar</h2>
        <form onSubmit={onSubmit}>
          <Field label="E-mail">
            <input className="form-control" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </Field>
          <Field label="Senha">
            <div className="input-group">
              <input
                className="form-control" type={showPassword ? 'text' : 'password'} required
                value={password} onChange={(e) => setPassword(e.target.value)}
              />
              <button
                type="button" className="btn btn-outline-secondary"
                aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'}
                onClick={() => setShowPassword((v) => !v)}
              >
                <i className={`bi ${showPassword ? 'bi-eye-slash' : 'bi-eye'}`} aria-hidden="true" />
              </button>
            </div>
          </Field>
          {error && <div className="alert alert-danger" role="alert">{error}</div>}
          <button className="btn btn-primary w-100" disabled={busy} type="submit">
            {busy ? 'Entrando…' : 'Entrar'}
          </button>
        </form>
        <p className="mt-3 small">
          Sem conta? <Link to="/cadastro">Cadastre-se</Link>
        </p>
      </div>
    </div>
  );
}
