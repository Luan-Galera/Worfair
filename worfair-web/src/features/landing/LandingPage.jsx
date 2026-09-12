import { Link } from 'react-router-dom';
import { feeExamples, money } from '../../utils/money';

export default function LandingPage() {
  return (
    <div>
      <section className="hero rounded p-4 p-md-5 mb-4">
        <p className="eyebrow">Marketplace de trabalhos e vagas</p>
        <h1 className="display-6 fw-bold">Contrate e preste serviços com taxa transparente</h1>
        <p className="lead">
          Empresas e pessoas publicam vagas de emprego e trabalhos freelancer, recebem propostas,
          contratam e pagam com mensagens e mediação embutidas.
        </p>
        <div className="d-flex gap-2 flex-wrap">
          <Link className="btn btn-primary" to="/trabalhos">Ver trabalhos</Link>
          <Link className="btn btn-outline-primary" to="/vagas">Ver vagas</Link>
          <Link className="btn btn-success" to="/contratar">Contratar / publicar</Link>
        </div>
      </section>

      <section className="row g-3 mb-4">
        <div className="col-md-4">
          <div className="card h-100"><div className="card-body">
            <h5><i className="bi bi-kanban me-2" />Trabalhos freelancer</h5>
            <p className="small text-muted">Projetos com orçamento, propostas de prestadores e aceite pelo contratante.</p>
            <Link to="/trabalhos">Explorar trabalhos</Link>
          </div></div>
        </div>
        <div className="col-md-4">
          <div className="card h-100"><div className="card-body">
            <h5><i className="bi bi-briefcase me-2" />Vagas de emprego (empresas)</h5>
            <p className="small text-muted">Vagas publicadas por empresas, com candidatura direta e acompanhamento.</p>
            <Link to="/vagas">Explorar vagas</Link>
          </div></div>
        </div>
        <div className="col-md-4">
          <div className="card h-100"><div className="card-body">
            <h5><i className="bi bi-percent me-2" />Taxa de 15%</h5>
            <p className="small text-muted">Sem surpresa: o prestador recebe o valor cheio, a taxa vai ao mantenedor.</p>
            <a href="#taxa">Ver tabela</a>
          </div></div>
        </div>
      </section>

      <section className="card mb-4" id="taxa">
        <div className="card-body">
          <h5>Exemplos com a taxa aplicada</h5>
          <div className="table-responsive">
            <table className="table table-sm mb-0">
              <thead><tr><th>Valor</th><th>Taxa (15%)</th><th>Contratante paga</th><th>Prestador recebe</th></tr></thead>
              <tbody>
                {feeExamples().map((e) => (
                  <tr key={e.amount}>
                    <td>{money(e.amount)}</td><td>{money(e.fee)}</td>
                    <td>{money(e.total)}</td><td>{money(e.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <div className="alert alert-info">
        O catálogo é aberto: qualquer visitante vê trabalhos e vagas. Para contratar, propor e
        pagar, <Link to="/cadastro">crie sua conta</Link> ou <Link to="/login">entre</Link>.
      </div>
    </div>
  );
}
