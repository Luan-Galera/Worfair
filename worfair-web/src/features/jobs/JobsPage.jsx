import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { jobsApi } from '../../api/endpoints/marketplace';
import { useAuth } from '../../auth/useAuth';
import { apiMessage } from '../../hooks/useQuery';
import { Skeleton, EmptyState, ErrorState } from '../../components/ui/Skeleton';
import { ShowcaseFilters } from './ShowcaseFilters';

const REMOTE = { 0: 'Presencial', 1: 'Híbrido', 2: 'Remoto' };

export default function JobsPage() {
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
      .listPostings(params)
      .then((res) => alive && setData(res.data ?? []))
      .catch((err) => alive && setError(err))
      .finally(() => alive && setLoading(false));
    return () => {
      alive = false;
    };
  }, [params]);

  const companies = useMemo(() => {
    const map = new Map();
    for (const j of data) {
      if (j.companyId && !map.has(j.companyId)) {
        map.set(j.companyId, { id: j.companyId, name: j.companyName ?? 'Empresa' });
      }
    }
    return [...map.values()];
  }, [data]);

  return (
    <div>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h2 className="mb-0">Vagas de emprego</h2>
        {isAuthenticated && <Link className="btn btn-sm btn-success" to="/contratar">Publicar vaga</Link>}
      </div>
      {!isAuthenticated && (
        <div className="alert alert-info">
          Vitrine pública — <Link to="/login">entre</Link> ou <Link to="/cadastro">crie sua conta</Link>{' '}
          para se candidatar e acompanhar.
        </div>
      )}
      <ShowcaseFilters kind="job" companies={companies} onChange={setParams} />
      {loading && <Skeleton lines={4} />}
      {error && <ErrorState message={apiMessage(error)} onRetry={() => setParams({ ...params })} />}
      {!loading && !error && data.length === 0 && (
        <EmptyState
          icon="bi-briefcase"
          title="Nenhuma vaga encontrada"
          hint="Ajuste os filtros ou publique a primeira vaga (empresas)."
        />
      )}
      <div className="row g-3">
        {data.map((j) => (
          <div key={j.id} className="col-12">
            <div className="card">
              <div className="card-body">
                <h5><Link to={`/vagas/${j.id}`}>{j.title}</Link></h5>
                <p className="mb-1">
                  {j.companyName && <><strong>{j.companyName}</strong> · </>}
                  <span className="text-muted">{j.location ?? 'Local a combinar'} · {REMOTE[j.remote] ?? '—'}</span>
                  {j.category && <span className="badge bg-secondary ms-2">{j.category}</span>}
                </p>
                <p className="mb-0 text-truncate text-muted">{j.description}</p>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
