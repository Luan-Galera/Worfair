import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { authApi } from '../api/endpoints/auth';
import { useAuth } from '../auth/AuthContext';

// Espelho do contexto calculado pelo BACKEND (GET /identity/me):
// roles, permissões efetivas, modo ATUAL e modos disponíveis.
// O modo é automático (derivado do tenant) — não há toggle manual.
export const MODES = {
  contracting: { label: 'Contratante', icon: 'bi-briefcase', color: 'primary' },
  provider: { label: 'Prestador', icon: 'bi-person-workspace', color: 'success' },
  global: { label: 'Plataforma', icon: 'bi-shield-check', color: 'dark' },
  none: { label: 'Sem espaço', icon: 'bi-hourglass', color: 'warning' },
};

const AccessContext = createContext(null);

export function AccessProvider({ children }) {
  const { user, isAuthenticated } = useAuth();
  const [me, setMe] = useState(null);
  const [loading, setLoading] = useState(false);

  const refreshMe = useCallback(async () => {
    if (!isAuthenticated) {
      setMe(null);
      return null;
    }
    setLoading(true);
    try {
      const { data } = await authApi.me();
      setMe(data);
      return data;
    } catch {
      setMe(null);
      return null;
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    refreshMe().catch(() => {});
  }, [refreshMe, user?.userId]);

  const can = useCallback(
    (permission) => (me?.effectivePermissions ?? []).includes(permission),
    [me],
  );

  const value = useMemo(
    () => ({
      me,
      loading,
      refreshMe,
      permissions: me?.effectivePermissions ?? [],
      roles: me?.roles ?? [],
      availableModes: me?.availableModes ?? [],
      activeMode: me?.mode ?? null,
      tenantId: me?.tenantId ?? null,
      memberships: me?.memberships ?? [],
      hasContext: Boolean(me?.tenantId) || (me?.roles ?? []).includes('SUPER_ADMIN'),
      isGlobalAdmin: (me?.roles ?? []).includes('SUPER_ADMIN') && !me?.tenantId,
      needsOnboarding:
        isAuthenticated &&
        Boolean(me) &&
        (me?.memberships ?? []).length === 0 &&
        !(me?.roles ?? []).includes('SUPER_ADMIN'),
      can,
    }),
    [me, loading, refreshMe, can, isAuthenticated],
  );

  return <AccessContext.Provider value={value}>{children}</AccessContext.Provider>;
}

export function useAccess() {
  const ctx = useContext(AccessContext);
  if (!ctx) throw new Error('useAccess deve ser usado dentro de <AccessProvider>');
  return ctx;
}
