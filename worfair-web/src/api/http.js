import axios from 'axios';
import { getAccessToken, getRefreshToken, persistTokens, clearTokens } from '../auth/tokenStorage';

// Instância única. baseURL vem do ambiente (produção) ou do proxy /api (dev).
// O frontend NUNCA envia X-Tenant-Id/tenant_id/mode em requisições de negócio:
// o backend deriva tudo do JWT (SEC-02/FE-02).
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  timeout: 15000,
});

http.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Refresh single-flight: 401s concorrentes aguardam o MESMO refresh e retentam
// a requisição original exatamente uma vez. Sem fila de callbacks (FE-05).
let refreshPromise = null;
const isRefreshRoute = (url = '') => url.includes('/identity/refresh');

http.interceptors.response.use(
  (res) => res,
  async (error) => {
    const { config, response } = error;
    if (!config || !response) return Promise.reject(error);

    if (response.status === 401 && !config._retried && !isRefreshRoute(config.url)) {
      config._retried = true;
      if (!refreshPromise) {
        refreshPromise = http
          .post('/identity/refresh', { refreshToken: getRefreshToken() })
          .then(({ data }) => {
            persistTokens(data);
            return data.accessToken;
          })
          .catch((err) => {
            clearTokens();
            window.dispatchEvent(new Event('auth:expired'));
            throw err;
          })
          .finally(() => {
            refreshPromise = null;
          });
      }
      try {
        const token = await refreshPromise;
        config.headers.Authorization = `Bearer ${token}`;
        return http(config);
      } catch (refreshErr) {
        return Promise.reject(refreshErr);
      }
    }

    if (response.status === 403) {
      // Backend negou: contexto (tenant/modo) ou permissão. A UI só orienta.
      window.dispatchEvent(new CustomEvent('auth:forbidden', { detail: response.data }));
    }

    return Promise.reject(error);
  },
);
