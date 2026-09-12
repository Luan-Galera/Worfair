import { Link } from 'react-router-dom';

export function ForbiddenPage() {
  return (
    <div className="alert alert-warning" role="alert">
      <h4>Acesso negado (403)</h4>
      <p>O backend recusou a operação para o seu contexto atual.</p>
      <Link className="btn btn-sm btn-outline-secondary" to="/painel">Voltar ao painel</Link>
    </div>
  );
}

export default ForbiddenPage;
