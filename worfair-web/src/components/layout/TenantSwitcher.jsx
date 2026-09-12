import { useState } from 'react';
import { useAuth } from '../../auth/useAuth';
import { useAccess } from '../../access/useAccess';

// Troca de CONTEXTO por tenant (única via; o modo deriva automaticamente).
export function TenantSwitcher() {
  const { switchTenant } = useAuth();
  const { memberships, tenantId, refreshMe } = useAccess();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  if (!memberships || memberships.length < 2) return null;

  const onChange = async (e) => {
    const id = e.target.value;
    if (!id || id === tenantId || busy) return;
    setBusy(true);
    setError(null);
    try {
      await switchTenant(id);
      await refreshMe();
    } catch {
      setError('Não foi possível trocar de espaço.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <span className="d-inline-flex align-items-center gap-1">
      <select
        className="form-select form-select-sm"
        aria-label="Trocar de espaço"
        value={tenantId ?? ''}
        onChange={onChange}
        disabled={busy}
      >
        {!tenantId && <option value="">Escolher espaço…</option>}
        {memberships.map((m) => (
          <option key={m.tenantId} value={m.tenantId}>
            {m.tenantName}
          </option>
        ))}
      </select>
      {error && <span className="text-danger small">{error}</span>}
    </span>
  );
}
