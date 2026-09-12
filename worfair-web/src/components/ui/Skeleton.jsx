export function Skeleton({ lines = 3 }) {
  return (
    <div aria-busy="true" aria-label="Carregando">
      {Array.from({ length: lines }).map((_, i) => (
        <div key={i} className="placeholder-glow mb-2">
          <span className="placeholder col-12" />
        </div>
      ))}
    </div>
  );
}

export function EmptyState({ icon = 'bi-inbox', title = 'Nada por aqui', hint = null, action = null }) {
  return (
    <div className="text-center text-muted py-5">
      <i className={`bi ${icon} fs-1`} aria-hidden="true" />
      <p className="fw-semibold mt-2 mb-1">{title}</p>
      {hint && <p className="small">{hint}</p>}
      {action}
    </div>
  );
}

export function ErrorState({ message = 'Falha ao carregar.', onRetry = null }) {
  return (
    <div className="alert alert-danger d-flex align-items-center gap-2" role="alert">
      <i className="bi bi-exclamation-triangle" aria-hidden="true" />
      <span className="flex-grow-1">{message}</span>
      {onRetry && (
        <button type="button" className="btn btn-sm btn-outline-danger" onClick={onRetry}>
          Tentar de novo
        </button>
      )}
    </div>
  );
}
