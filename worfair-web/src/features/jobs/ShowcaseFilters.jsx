import { useEffect, useState } from 'react';
import { CATEGORIES } from '../../utils/categories';

// Barra de filtros da vitrine (busca por nome + categoria + empresa + extras).
// kind=job → tipo (presencial/híbrido/remoto); kind=project → faixa de orçamento.
export function ShowcaseFilters({ kind, companies, onChange }) {
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('');
  const [companyId, setCompanyId] = useState('');
  const [remote, setRemote] = useState('');
  const [minBudget, setMinBudget] = useState('');
  const [maxBudget, setMaxBudget] = useState('');

  // Debounce só no texto; demais filtros aplicam na hora.
  const [debounced, setDebounced] = useState('');
  useEffect(() => {
    const t = setTimeout(() => setDebounced(search.trim()), 400);
    return () => clearTimeout(t);
  }, [search]);

  useEffect(() => {
    const params = {};
    if (debounced) params.search = debounced;
    if (category) params.category = category;
    if (companyId) params.companyId = companyId;
    if (kind === 'job' && remote !== '') params.remote = Number(remote);
    if (kind === 'project' && minBudget !== '') params.minBudget = Number(minBudget);
    if (kind === 'project' && maxBudget !== '') params.maxBudget = Number(maxBudget);
    onChange(params);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debounced, category, companyId, remote, minBudget, maxBudget, kind]);

  const clear = () => {
    setSearch('');
    setCategory('');
    setCompanyId('');
    setRemote('');
    setMinBudget('');
    setMaxBudget('');
  };

  return (
    <div className="card mb-3">
      <div className="card-body">
        <div className="row g-2">
          <div className="col-md-4">
            <input
              className="form-control"
              placeholder="Pesquisar pelo nome…"
              aria-label="Pesquisar pelo nome"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <div className="col-md-3">
            <select className="form-select" aria-label="Categoria" value={category} onChange={(e) => setCategory(e.target.value)}>
              <option value="">Todas as categorias</option>
              {CATEGORIES.map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </select>
          </div>
          <div className="col-md-3">
            <select className="form-select" aria-label="Empresa" value={companyId} onChange={(e) => setCompanyId(e.target.value)}>
              <option value="">Todas as empresas</option>
              {(companies ?? []).map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
          </div>
          {kind === 'job' ? (
            <div className="col-md-2">
              <select className="form-select" aria-label="Tipo" value={remote} onChange={(e) => setRemote(e.target.value)}>
                <option value="">Todos os tipos</option>
                <option value="0">Presencial</option>
                <option value="1">Híbrido</option>
                <option value="2">Remoto</option>
              </select>
            </div>
          ) : (
            <>
              <div className="col-md-1">
                <input className="form-control" type="number" min="0" placeholder="Min R$" aria-label="Orçamento mínimo" value={minBudget} onChange={(e) => setMinBudget(e.target.value)} />
              </div>
              <div className="col-md-1">
                <input className="form-control" type="number" min="0" placeholder="Max R$" aria-label="Orçamento máximo" value={maxBudget} onChange={(e) => setMaxBudget(e.target.value)} />
              </div>
            </>
          )}
        </div>
        <button type="button" className="btn btn-sm btn-outline-secondary mt-2" onClick={clear}>
          Limpar filtros
        </button>
      </div>
    </div>
  );
}
