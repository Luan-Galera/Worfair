import { http } from '../http';

// Identity: register/login/refresh/logout/me/switch-tenant.
// NÃO existe switch-mode: o modo é derivado automaticamente pelo backend
// (ModeResolver) a partir do tenant ativo.
export const authApi = {
  register: (payload) => http.post('/identity/register', payload),
  login: (payload) => http.post('/identity/login', payload),
  refresh: (refreshToken) => http.post('/identity/refresh', { refreshToken }),
  logout: (refreshToken) => http.post('/identity/logout', { refreshToken }),
  me: () => http.get('/identity/me'),
  switchTenant: (targetTenantId) =>
    http.post('/identity/switch-tenant', { targetTenantId }),
  bootstrap: (workspaceName) =>
    http.post('/tenants/bootstrap', { workspaceName }),
};
