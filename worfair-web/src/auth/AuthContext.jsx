import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { authApi } from '../api/endpoints/auth';
import {
  clearTokens,
  getCachedUser,
  getRefreshToken,
  persistTokens,
  setAccessToken,
} from './tokenStorage';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => getCachedUser());
  const [tokens, setTokens] = useState(null); // { accessToken, refreshToken }
  const [loading, setLoading] = useState(false);
  const [initialized, setInitialized] = useState(false);

  const applyAuthResponse = useCallback((data) => {
    persistTokens(data);
    setTokens({ accessToken: data.accessToken, refreshToken: data.refreshToken });
    const u = { userId: data.userId, email: data.email, fullName: data.fullName };
    setUser(u);
    return u;
  }, []);

  const login = useCallback(
    async (email, password, targetTenantId) => {
      setLoading(true);
      try {
        const { data } = await authApi.login({ email, password, targetTenantId });
        return applyAuthResponse(data);
      } finally {
        setLoading(false);
      }
    },
    [applyAuthResponse],
  );

  const register = useCallback(async (email, password, fullName, userType = 1, document = '', phone = null) => {
    setLoading(true);
    try {
      const { data } = await authApi.register({ email, password, fullName, userType, document, phone });
      return data; // Guid do usuário criado
    } finally {
      setLoading(false);
    }
  }, []);

  const logout = useCallback(async () => {
    const rt = tokens?.refreshToken ?? getRefreshToken();
    try {
      if (rt) await authApi.logout(rt);
    } catch {
      // logout best-effort
    }
    clearTokens();
    setTokens(null);
    setUser(null);
  }, [tokens]);

  const switchTenant = useCallback(
    async (targetTenantId) => {
      const { data } = await authApi.switchTenant(targetTenantId);
      applyAuthResponse(data);
      return data;
    },
    [applyAuthResponse],
  );

  useEffect(() => {
    const onExpired = () => {
      clearTokens();
      setTokens(null);
      setUser(null);
    };
    window.addEventListener('auth:expired', onExpired);
    setInitialized(true);
    return () => window.removeEventListener('auth:expired', onExpired);
  }, []);

  const value = useMemo(
    () => ({
      user,
      tokens,
      loading,
      initialized,
      isAuthenticated: Boolean(user && (tokens?.accessToken || getRefreshToken())),
      login,
      register,
      logout,
      switchTenant,
      applyAuthResponse,
      setAccessToken,
    }),
    [user, tokens, loading, initialized, login, register, logout, switchTenant, applyAuthResponse],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth deve ser usado dentro de <AuthProvider>');
  return ctx;
}
