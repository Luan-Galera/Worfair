import { useState } from 'react';
import { tenantsApi } from '../../api/endpoints/marketplace';
import { useAccess } from '../../access/useAccess';
import { useQuery, apiMessage } from '../../hooks/useQuery';
import { Skeleton, EmptyState, ErrorState } from '../../components/ui/Skeleton';
import { Field } from '../../components/ui/widgets';
import { RequirePermission } from '../../routing/RequirePermission';

// Cadastro de empresas do espaço. Só empresa publica VAGA de emprego;
// usuário sem empresa publica trabalhos freelancer.
export default function CompanyPage() {
  return (
    <RequirePermission permission="tenants.settings.read">
      <CompanyContent />
    </RequirePermission>
  );
}

function CompanyContent() {
  const { me, can } = useAccess();
  const q = useQuery(() => tenantsApi.companies());
  const members = useQuery(() => tenantsApi.members().catch(() => ({ data: [] })));
  const [form, setForm] = useState({ legalName: '', tradeName: '', document: '', email: '', phone: '' });
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState(null);
  const [transfer, setTransfer] = useState({});

  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));

  const doTransfer = async (companyId) => {
    const target = transfer[companyId];
    if (!target) return;
    setBusy(true);
    setFeedback(null);
    try {
      await tenantsApi.transferCompanyOwner(companyId, target);
      setFeedback({ ok: true, text: 'Vínculo transferido!' });
      await q.refetch();
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true);
    setFeedback(null);
    try {
      await tenantsApi.createCompany({
        legalName: form.legalName.trim(),
        tradeName: form.tradeName.trim() || null,
        document: form.document.replace(/\D/g, ''),
        email: form.email.trim() || null,
        phone: form.phone.trim() || null,
      });
      setFeedback({ ok: true, text: 'Empresa cadastrada! Agora você pode publicar vagas.' });
      setForm({ legalName: '', tradeName: '', document: '', email: '', phone: '' });
      await q.refetch();
    } catch (err) {
      setFeedback({ ok: false, text: apiMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <h2>Empresa</h2>
      <p className="text-muted">
        Vagas de emprego são atos de empresa: para publicar uma vaga, cadastre aqui a empresa do
        espaço (CNPJ com 14 dígitos ou CPF com 11). Trabalhos freelancer não exigem empresa.
      </p>

      <h5>Empresas do espaço ({(q.data ?? []).length})</h5>
      {q.loading && <Skeleton lines={2} />}
      {q.error && <ErrorState message={apiMessage(q.error)} onRetry={() => q.refetch().catch(() => {})} />}
      {!q.loading && !q.error && (q.data ?? []).length === 0 && (
        <EmptyState icon="bi-building" title="Nenhuma empresa" hint="Cadastre a primeira abaixo." />
      )}
      <div className="list-group mb-4">
        {(q.data ?? []).map((c) => (
          <div key={c.id} className="list-group-item">
            <strong>{c.tradeName || c.legalName}</strong>
            <div className="small text-muted">{c.legalName} · doc. {c.document}</div>
            <div className="small">
              Responsável vinculado:{' '}
              {c.ownerUserId ? (
                <code>{c.ownerUserId}</code>
              ) : (
                <span className="text-muted">nenhum</span>
              )}
              {me?.userId === c.ownerUserId && <span className="badge bg-primary ms-2">você</span>}
            </div>
            {can('tenants.members.manage') && (
              <div className="d-flex gap-2 mt-2">
                <select
                  className="form-select form-select-sm"
                  aria-label={`Transferir ${c.tradeName || c.legalName}`}
                  value={transfer[c.id] ?? ''}
                  onChange={(e) => setTransfer((t) => ({ ...t, [c.id]: e.target.value }))}
                >
                  <option value="">Transferir vínculo para…</option>
                  {(members.data ?? []).map((m) => (
                    <option key={m.userId} value={m.userId}>{m.userId}</option>
                  ))}
                </select>
                <button
                  type="button"
                  className="btn btn-sm btn-outline-secondary"
                  disabled={busy || !transfer[c.id]}
                  onClick={() => doTransfer(c.id)}
                >
                  Transferir
                </button>
              </div>
            )}
          </div>
        ))}
      </div>

      <div className="card">
        <div className="card-body">
          <h5>Cadastrar empresa</h5>
          <form onSubmit={submit}>
            <Field label="Razão social / nome">
              <input className="form-control" required maxLength={200} value={form.legalName} onChange={set('legalName')} />
            </Field>
            <Field label="Nome fantasia (opcional)">
              <input className="form-control" maxLength={200} value={form.tradeName} onChange={set('tradeName')} />
            </Field>
            <div className="row">
              <div className="col-md-4">
                <Field label="CNPJ ou CPF (só números)" hint="14 dígitos (CNPJ) ou 11 (CPF).">
                  <input className="form-control" required inputMode="numeric" value={form.document} onChange={set('document')} />
                </Field>
              </div>
              <div className="col-md-4">
                <Field label="E-mail (opcional)">
                  <input className="form-control" type="email" value={form.email} onChange={set('email')} />
                </Field>
              </div>
              <div className="col-md-4">
                <Field label="Telefone (opcional)">
                  <input className="form-control" value={form.phone} onChange={set('phone')} />
                </Field>
              </div>
            </div>
            {feedback && (
              <div className={`alert ${feedback.ok ? 'alert-success' : 'alert-danger'}`} role="alert">
                {feedback.text}
              </div>
            )}
            <button className="btn btn-primary" type="submit" disabled={busy}>
              {busy ? 'Cadastrando…' : 'Cadastrar empresa'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
