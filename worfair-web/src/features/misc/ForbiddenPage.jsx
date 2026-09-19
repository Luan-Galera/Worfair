import { Link } from 'react-router-dom';

export function ForbiddenPage() {
  return (
    <div className="alert alert-warning" role="alert">
      <h4>Sem acesso</h4>
      <p>Esta área não está disponível para o seu contexto atual.</p>
      <Link className="btn btn-sm btn-outline-secondary" to="/">Voltar ao início</Link>
    </div>
  );
}

export default ForbiddenPage;
