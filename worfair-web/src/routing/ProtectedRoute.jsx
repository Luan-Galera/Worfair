import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { useAccess } from '../access/useAccess';
import { PermissionDenied } from '../components/feedback/PermissionDenied';
import { Skeleton } from '../components/ui/Skeleton';

// Guard composto: autenticação → contexto (tenant) → permissão efetiva.
// O modo é automático (vem do /me); a troca de contexto é por tenant.
export function ProtectedRoute({ permission = null, requireContext = true }) {
  const { isAuthenticated, initialized } = useAuth();
  const { can, loading, hasContext, needsOnboarding } = useAccess();
  const location = useLocation();

  if (!initialized || loading) return <Skeleton lines={3} />;

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  if (requireContext && needsOnboarding) {
    return <Navigate to="/onboarding" replace />;
  }

  if (requireContext && !hasContext) {
    return <Navigate to="/onboarding" replace />;
  }

  if (permission && !can(permission)) {
    return <PermissionDenied reason="permissão insuficiente para este contexto" />;
  }

  return <Outlet />;
}
