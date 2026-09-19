import { Link, useNavigate } from 'react-router-dom';
import { useAccess } from '../../access/useAccess';

// Tela de bloqueio por contexto/permissão — sem jargão técnico:
// orienta o próximo passo (criar/entrar no espaço ou pedir acesso).
export function PermissionDenied() {
  const { isGlobalAdmin, tenantId } = useAccess();
  const navigate = useNavigate();

  let title = 'Sem acesso aqui';
  let hint =
    'Seu perfil neste espaço não tem permissão para esta área. Peça acesso ao responsável pelo espaço.';
  let action = null;

  if (isGlobalAdmin) {
    title = 'Área dos espaços';
    hint =
      'Você está no contexto da plataforma. Para publicar vagas e trabalhos, primeiro crie seu espaço pessoal e entre nele.';
    action = (
      <Link className="btn btn-sm btn-primary" to="/onboarding">
        Criar meu espaço
      </Link>
    );
  } else if (!tenantId) {
    title = 'Entre em um espaço';
    hint = 'Esta área pertence a um espaço. Crie seu espaço pessoal ou entre em um existente.';
    action = (
      <Link className="btn btn-sm btn-primary" to="/onboarding">
        Ir para meus espaços
      </Link>
    );
  }

  return (
    <div className="alert alert-warning" role="alert">
      <h5 className="alert-heading">
        <i className="bi bi-lock me-2" aria-hidden="true" />
        {title}
      </h5>
      <p className="mb-3">{hint}</p>
      <div className="d-flex gap-2">
        {action}
        <button
          type="button"
          className="btn btn-sm btn-outline-secondary"
          onClick={() => navigate(-1)}
        >
          Voltar
        </button>
      </div>
    </div>
  );
}
