import { Link } from 'react-router-dom';
import { money } from '../../utils/money';

export function MoneyText({ value, currency }) {
  return <span>{money(value, currency)}</span>;
}

const STATUS_COLORS = {
  // Propostas / candidaturas (numérico do backend)
  1: 'secondary', // Submitted/Draft/Issued/Open
  2: 'success', // Accepted/Published/Paid
  3: 'danger', // Rejected/Closed
  4: 'dark', // Archived
  // Faturas / disputas (string do backend)
  Issued: 'warning',
  Paid: 'success',
  Reversed: 'secondary',
  Open: 'warning',
  UnderMediation: 'info',
  Resolved: 'success',
  Rejected: 'danger',
};

export function StatusBadge({ value, label }) {
  const color = STATUS_COLORS[value] ?? 'secondary';
  return <span className={`badge bg-${color}`}>{label ?? String(value)}</span>;
}

export function FeePreview({ amount, currency = 'BRL' }) {
  const a = Number(amount || 0);
  if (!a || a <= 0) return null;
  const fee = Math.round(a * 0.15 * 100) / 100;
  const total = Math.round(a * 1.15 * 100) / 100;
  return (
    <div className="alert alert-warning small mb-0" role="note">
      Valor do trabalho: <strong>{money(a, currency)}</strong>
      {' '}· taxa 15%: <strong>{money(fee, currency)}</strong>
      {' '}· contratante paga: <strong>{money(total, currency)}</strong>
      {' '}· prestador recebe: <strong>{money(a, currency)}</strong>.
    </div>
  );
}

export function Field({ label, hint, error, children }) {
  return (
    <div className="mb-3">
      <label className="form-label">{label}</label>
      {children}
      {hint && <div className="form-text">{hint}</div>}
      {error && <div className="text-danger small">{error}</div>}
    </div>
  );
}
