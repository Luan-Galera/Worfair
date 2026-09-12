// Access token em memória (nunca em storage persistente); refresh + perfil
// mínimo em localStorage para sobreviver ao reload. Sem segredos no código.
let accessToken = null;

const REFRESH_KEY = 'worfair.refreshToken';
const USER_KEY = 'worfair.user';

export function getAccessToken() {
  return accessToken;
}

export function getRefreshToken() {
  try {
    return localStorage.getItem(REFRESH_KEY);
  } catch {
    return null;
  }
}

export function persistTokens(data) {
  if (!data) return;
  if (data.accessToken) accessToken = data.accessToken;
  try {
    if (data.refreshToken) localStorage.setItem(REFRESH_KEY, data.refreshToken);
    if (data.userId) {
      localStorage.setItem(
        USER_KEY,
        JSON.stringify({ userId: data.userId, email: data.email, fullName: data.fullName }),
      );
    }
  } catch {
    // storage indisponível: sessão segue só em memória
  }
}

export function setAccessToken(token) {
  accessToken = token ?? null;
}

export function getCachedUser() {
  try {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function clearTokens() {
  accessToken = null;
  try {
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
  } catch {
    // ignore
  }
}
