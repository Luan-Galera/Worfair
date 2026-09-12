import { Link } from 'react-router-dom';
import { useQuery } from '../../hooks/useQuery';
import { financialApi, tenantsApi } from '../../api/endpoints/marketplace';
import { Skeleton } from '../../components/ui/Skeleton';
import { money } from '../../utils/money';

// Painel exclusivo do super admin: visão da plataforma (receita em taxas,
// mediação e espaços). Usuários comuns não veem esta página (rota exige
// platform.tenants.manage).
export default function DashboardPage() {
  const revenue = useQuery(() => financialApi.revenue().catch(() => ({ data: null })));
  const tenants = useQuery(() => tenantsApi.listPlatform().catch(() => ({ data: [] })));
  const disputes = useQuery(() => financialApi.disputes().catch(() => ({ data: [] })));
  const r = revenue.data;
  const open = (disputes.data ?? []).filter((d) => d.status === 'Open' || d.status === 'UnderMediation').length;

  return (
    <div>
      <h2>Painel da plataforma</h2>
      <p className="text-muted">Faturamento em taxas e atividade — só super admin.</p>
      {revenue.loading ? <Skeleton lines={2} /> : r && (
        <div className="row g-3 mb-4">
          <div className="col-md-4">
            <div className="card"><div className="card-body">
              <h6 className="text-muted">Taxas recebidas</h6>
              <p className="fs-4 mb-0">{money(r.feesReceived, r.currency)}</p>
              <small className="text-muted">{r.paidCount} fatura(s) pagas</small>
            </div></div>
          </div>
          <div className="col-md-4">
            <div className="card"><div className="card-body">
              <h6 className="text-muted">Taxas a receber</h6>
              <p className="fs-4 mb-0">{money(r.feesPending, r.currency)}</p>
              <small className="text-muted">{r.issuedCount} em aberto · volume {money(r.volumePaid, r.currency)}</small>
            </div></div>
          </div>
          <div className="col-md-4">
            <div className="card"><div className="card-body">
              <h6 className="text-muted">Mediação</h6>
              <p className="fs-4 mb-0">{open} aberta(s)</p>
              <small className="text-muted">{(tenants.data ?? []).length} espaço(s) ativos</small>
            </div></div>
          </div>
        </div>
      )}
      <div className="d-flex gap-2">
        <Link className="btn btn-dark" to="/plataforma">Plataforma (tenants + disputas)</Link>
        <Link className="btn btn-outline-dark" to="/financeiro">Faturas</Link>
      </div>
    </div>
  );
}
