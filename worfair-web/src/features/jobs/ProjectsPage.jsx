import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { jobsApi } from '../../api/endpoints/marketplace';
import { useAuth } from '../../auth/useAuth';
import { apiMessage } from '../../hooks/useQuery';
import { Skeleton, EmptyState, ErrorState } from '../../components/ui/Skeleton';
import { ShowcaseFilters } from './ShowcaseFilters';
import { money } from '../../utils/money';

export default function ProjectsPage() {
  const { isAuthenticated } = useAuth();
  const [params, setParams] = useState({});
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let alive = true;
    setLoading(true);
    setError(null);
    jobsApi
      .listProjects(params)
      .then((res) => alive && setData(res.data ?? []))
      .catch((err) => alive && setError(err))
      .finally(() => alive && setLoading(false));
    return () => {
      alive = false;
    };
  }, [params]);

  const companies = useMemo(() => {
    const map = new Map();
    for (const p of data) {
      if (p.companyId && !map.has(p.companyId)) {
        map.set(p.companyId, { id: p.companyId, name: p.companyName ?? 'Empresa' });
      }
    }
    return [...map.values()];
  }, [data]);

  return (
    <div>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2 className="mb-0">Trabalhos freelancer</h2>
        {isAuthenticated && <Link className="btn btn-sm btn-success" to="/contratar">Publicar trabalho</Link>}
      </div>
      {!isAuthenticated && (
        <div className="alert alert-info">
          Vitrine pública — <Link to="/login">entre</Link> ou <Link to="/cadastro">crie sua conta</Link>{' '}
          para enviar propostas e acompanhar.
        </div>
      )}
      <ShowcaseFilters kind="project" companies={companies} onChange={setParams} />
      {loading && <Skeleton lines={4} />}
      {error && <ErrorState message={apiMessage(error)} onRetry={() => setParams({ ...params })} />}
      {!loading && !error && data.length === 0 && (
        <EmptyState
          icon="bi-kanban"
          title="Nenhum trabalho encontrado"
          hint="Ajuste os filtros ou publique o primeiro trabalho."
        />
      )}
      <div className="row g-3">
        {data.map((p) => (
          <div key={p.id} className="col-12">
            <div className="card">
              <div className="card-body">
                <h5><Link to={`/trabalhos/${p.id}`}>{p.title}</Link></h5>
                <p className="mb-1">
                  {p.companyName && <><strong>{p.companyName}</strong> · </>}
                  <strong>{money(p.budgetMin, p.currency)}</strong>
                  {p.budgetMax ? <> até <strong>{money(p.budgetMax, p.currency)}</strong></> : null}
                  {p.category && <span className="badge bg-secondary ms-2">{p.category}</span>}
                </p>
                <p className="mb-0 text-truncate text-muted">{p.description}</p>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
