import { Link } from 'react-router-dom';

export function PermissionDenied({ reason = 'acesso negado' }) {
  return (
    <div className="alert alert-warning" role="alert">
      <h5 className="alert-heading">
        <i className="bi bi-lock me-2" aria-hidden="true" />
        Sem acesso neste contexto
      </h5>
      <p className="mb-2">
        O backend negou a operação ({reason}). Troque de espaço no seletor acima ou peça acesso ao
        responsável.
      </p>
      <Link className="btn btn-sm btn-outline-secondary" to="/painel">
        Voltar ao painel
      </Link>
    </div>
  );
}
