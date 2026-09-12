import { useAccess } from '../access/useAccess';
import { PermissionDenied } from '../components/feedback/PermissionDenied';

// Guard em nível de elemento (reforça o ProtectedRoute dentro da página).
export function RequirePermission({ permission, children }) {
  const { can } = useAccess();
  if (permission && !can(permission)) {
    return <PermissionDenied reason="permissão insuficiente" />;
  }
  return children;
}
