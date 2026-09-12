import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <div className="text-center py-5">
      <h2>Página não encontrada</h2>
      <Link className="btn btn-primary mt-2" to="/">Voltar ao início</Link>
    </div>
  );
}

export default NotFoundPage;
