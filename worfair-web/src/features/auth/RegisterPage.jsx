import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/useAuth';
import { Field } from '../../components/ui/widgets';
import { apiMessage } from '../../hooks/useQuery';

export default function RegisterPage() {
  const { register, login } = useAuth();
  const navigate = useNavigate();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [userType, setUserType] = useState('1');
  const [document, setDocument] = useState('');
  const [phone, setPhone] = useState('');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async (e) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await register(
        email.trim(),
        password,
        fullName.trim(),
        Number(userType),
        document.replace(/\D/g, ''),
        phone.trim() || null,
      );
      // Entra direto e segue para o onboarding (criar o espaço pessoal).
      await login(email.trim(), password);
      navigate('/onboarding', { replace: true });
    } catch (err) {
      setError(apiMessage(err, 'Cadastro falhou. Verifique os dados.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="row justify-content-center">
      <div className="col-12 col-md-6">
        <h2>Criar conta</h2>
        <p className="text-muted small">
          Conta de pessoa ou de empresa. Cargos e vínculos (dono, equipe) são definidos dentro do
          espaço, após o cadastro.
        </p>
        <form onSubmit={onSubmit}>
          <Field label="Nome completo / razão social">
            <input className="form-control" required maxLength={200} value={fullName} onChange={(e) => setFullName(e.target.value)} />
          </Field>
          <div className="row">
            <div className="col-md-6">
              <Field label="Tipo de conta">
                <select className="form-select" value={userType} onChange={(e) => setUserType(e.target.value)}>
                  <option value="1">Pessoa física</option>
                  <option value="2">Empresa</option>
                </select>
              </Field>
            </div>
            <div className="col-md-6">
              <Field label={userType === '2' ? 'CNPJ (só números)' : 'CPF (só números)'}>
                <input
                  className="form-control" required inputMode="numeric"
                  value={document} onChange={(e) => setDocument(e.target.value)}
                  placeholder={userType === '2' ? '14 dígitos' : '11 dígitos'}
                />
              </Field>
            </div>
          </div>
          <div className="row">
            <div className="col-md-6">
              <Field label="E-mail">
                <input className="form-control" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
              </Field>
            </div>
            <div className="col-md-6">
              <Field label="Telefone (opcional)">
                <input className="form-control" value={phone} onChange={(e) => setPhone(e.target.value)} />
              </Field>
            </div>
          </div>
          <Field label="Senha" hint="Mínimo de 8 caracteres.">
            <div className="input-group">
              <input
                className="form-control" type={showPassword ? 'text' : 'password'} required minLength={8}
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
          <button className="btn btn-success w-100" disabled={busy} type="submit">
            {busy ? 'Criando…' : 'Cadastrar'}
          </button>
        </form>
        <p className="mt-3 small">
          Já tem conta? <Link to="/login">Entre</Link>
        </p>
      </div>
    </div>
  );
}
