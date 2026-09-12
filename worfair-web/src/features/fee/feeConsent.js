// Aceite explícito da taxa de 15% (LGPD/consumidor): a faixa pode ser
// dispensada SOMENTE depois do aceite. Sem aceite, ela reaparece sempre e as
// ações com dinheiro exigem o aceite via modal.
const CONSENT_KEY = 'worfair.feeConsent.v1';
const DISMISS_KEY = 'worfair.feeBanner.dismissed.v1';

function read(key) {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key, value) {
  try {
    localStorage.setItem(key, value);
  } catch {
    // storage indisponível: consentimento vale só para a sessão
  }
}

export function getFeeConsent() {
  try {
    const raw = read(CONSENT_KEY);
    if (!raw) return { accepted: false, at: null };
    const parsed = JSON.parse(raw);
    return { accepted: parsed.accepted === true, at: parsed.at ?? null };
  } catch {
    return { accepted: false, at: null };
  }
}

export function acceptFeeConsent() {
  write(CONSENT_KEY, JSON.stringify({ accepted: true, at: new Date().toISOString() }));
}

export function isFeeBannerDismissed() {
  return read(DISMISS_KEY) === '1';
}

export function dismissFeeBanner() {
  write(DISMISS_KEY, '1');
}
