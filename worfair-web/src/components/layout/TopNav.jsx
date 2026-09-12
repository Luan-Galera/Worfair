import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/useAuth';
import { useAccess, MODES } from '../../access/useAccess';
import { TenantSwitcher } from './TenantSwitcher';

function ModeBadge() {
  const { activeMode, availableModes } = useAccess();
  if (!activeMode) return null;
  const meta = MODES[activeMode] ?? { label: activeMode, icon: 'bi-circle', color: 'secondary' };
  return (
    <span className={`badge bg-${meta.color}`} title={`Modo automático do contexto: ${availableModes.join(', ')}`}>
      <i className={`bi ${meta.icon} me-1`} aria-hidden="true" />
      {meta.label} · automático
    </span>
  );
}

export function TopNav() {
  const { isAuthenticated, user, logout } = useAuth();
  const navigate = useNavigate();

  const onLogout = async () => {
    await logout();
    navigate('/');
  };

  return (
    <nav className="navbar navbar-expand-lg navbar-light bg-white border-bottom px-3">
      <Link className="navbar-brand fw-bold" to="/">
        Worfair
      </Link>
      <button
        className="navbar-toggler"
        type="button"
        data-bs-toggle="collapse"
        data-bs-target="#wfnav"
        aria-controls="wfnav"
        aria-expanded="false"
        aria-label="Alternar navegação"
      >
        <span className="navbar-toggler-icon" />
      </button>
      <div className="collapse navbar-collapse" id="wfnav">
        <ul className="navbar-nav me-auto mb-2 mb-lg-0">
          <li className="nav-item">
            <Link className="nav-link" to="/trabalhos">Trabalhos</Link>
          </li>
          <li className="nav-item">
            <Link className="nav-link" to="/vagas">Vagas</Link>
          </li>
          {isAuthenticated && (
            <>
              <li className="nav-item">
                <Link className="nav-link" to="/propostas">Propostas</Link>
              </li>
            </>
          )}
        </ul>
        <div className="d-flex align-items-center gap-2">
          {isAuthenticated && (
            <>
              <ModeBadge />
              <TenantSwitcher />
              <span className="text-muted small d-none d-md-inline">{user?.fullName ?? user?.email}</span>
              <button type="button" className="btn btn-sm btn-outline-secondary" onClick={onLogout}>
                Sair
              </button>
            </>
          )}
          {!isAuthenticated && (
            <>
              <Link className="btn btn-sm btn-outline-primary" to="/login">Entrar</Link>
              <Link className="btn btn-sm btn-primary" to="/cadastro">Cadastrar</Link>
            </>
          )}
        </div>
      </div>
    </nav>
  );
}
