// Taxa da plataforma: espelho exato do backend
// (FinancialInvoice.PlatformFeeRate = 0.15m; ProposalQueries usa 0.15).
export const PLATFORM_FEE_RATE = 0.15;

export function round2(value) {
  return Math.round((Number(value) + Number.EPSILON) * 100) / 100;
}

/** Contratante paga: valor + 15%. */
export function totalWithFee(amount) {
  return round2(Number(amount) * (1 + PLATFORM_FEE_RATE));
}

/** Taxa sobre o valor. */
export function feeOf(amount) {
  return round2(Number(amount) * PLATFORM_FEE_RATE);
}

/** Prestador recebe o valor cheio; a taxa vai ao mantenedor. */
export function providerReceives(amount) {
  return round2(Number(amount));
}

const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

export function money(value, currency = 'BRL') {
  const n = Number(value ?? 0);
  if (!currency || currency === 'BRL') return brl.format(n);
  try {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency }).format(n);
  } catch {
    return `${n.toFixed(2)} ${currency}`;
  }
}

export function feeExamples() {
  return [100, 500, 1000, 5000].map((v) => ({
    amount: v,
    fee: feeOf(v),
    total: totalWithFee(v),
  }));
}
