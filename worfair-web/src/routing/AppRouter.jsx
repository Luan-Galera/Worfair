import { Suspense, lazy } from 'react';
import { Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './ProtectedRoute';
import { PublicLayout } from '../layouts/PublicLayout';
import { AppLayout } from '../layouts/AppLayout';
import { Skeleton } from '../components/ui/Skeleton';

const LandingPage = lazy(() => import('../features/landing/LandingPage'));
const LoginPage = lazy(() => import('../features/auth/LoginPage'));
const RegisterPage = lazy(() => import('../features/auth/RegisterPage'));
const OnboardingPage = lazy(() => import('../features/onboarding/OnboardingPage'));
const DashboardPage = lazy(() => import('../features/dashboard/DashboardPage'));
const JobsPage = lazy(() => import('../features/jobs/JobsPage'));
const ProjectsPage = lazy(() => import('../features/jobs/ProjectsPage'));
const JobDetailPage = lazy(() => import('../features/jobs/JobDetailPage'));
const PublishPage = lazy(() => import('../features/publish/PublishPage'));
const CompanyPage = lazy(() => import('../features/company/CompanyPage'));
const TeamPage = lazy(() => import('../features/team/TeamPage'));
const ProposalsPage = lazy(() => import('../features/proposals/ProposalsPage'));
const FinancialPage = lazy(() => import('../features/financial/FinancialPage'));
const MessagesPage = lazy(() => import('../features/messages/MessagesPage'));
const NotificationsPage = lazy(() => import('../features/notifications/NotificationsPage'));
const PlatformPage = lazy(() => import('../features/platform/PlatformPage'));
const ForbiddenPage = lazy(() => import('../features/misc/ForbiddenPage'));
const NotFoundPage = lazy(() => import('../features/misc/NotFoundPage'));

function WithSuspense({ children }) {
  return <Suspense fallback={<Skeleton lines={4} />}>{children}</Suspense>;
}

export function AppRouter() {
  return (
    <WithSuspense>
      <Routes>
        {/* Públicas */}
        <Route element={<PublicLayout />}>
          <Route path="/" element={<LandingPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/cadastro" element={<RegisterPage />} />
          <Route path="/forbidden" element={<ForbiddenPage />} />
          {/* Vitrine pública (sem login): catálogo de vagas e trabalhos */}
          <Route path="/vagas" element={<JobsPage />} />
          <Route path="/vagas/:id" element={<JobDetailPage kind="job" />} />
          <Route path="/trabalhos" element={<ProjectsPage />} />
          <Route path="/trabalhos/:id" element={<JobDetailPage kind="project" />} />
        </Route>

        {/* Onboarding: autenticado, sem exigir contexto */}
        <Route element={<ProtectedRoute requireContext={false} />}>
          <Route element={<AppLayout />}>
            <Route path="/onboarding" element={<OnboardingPage />} />
          </Route>
        </Route>

        {/* Marketplace (exige login + contexto; backend autoriza por permissão) */}
        <Route element={<ProtectedRoute />}>
          <Route element={<AppLayout />}>
            <Route path="/propostas" element={<ProposalsPage />} />
            <Route path="/mensagens" element={<MessagesPage />} />
            <Route path="/notificacoes" element={<NotificationsPage />} />
          </Route>
        </Route>

        {/* Equipe e cargos (gestão do espaço) */}
        <Route element={<ProtectedRoute permission="tenants.members.manage" />}>
          <Route element={<AppLayout />}>
            <Route path="/equipe" element={<TeamPage />} />
          </Route>
        </Route>

        {/* Publicar (contratante com empresa no espaço) */}
        <Route element={<ProtectedRoute permission="tenants.settings.read" />}>
          <Route element={<AppLayout />}>
            <Route path="/contratar" element={<PublishPage />} />
            <Route path="/empresa" element={<CompanyPage />} />
          </Route>
        </Route>

        {/* Plataforma (super admin): painel, faturas, tenants e disputas */}
        <Route element={<ProtectedRoute permission="platform.tenants.manage" />}>
          <Route element={<AppLayout />}>
            <Route path="/painel" element={<DashboardPage />} />
            <Route path="/financeiro" element={<FinancialPage />} />
            <Route path="/plataforma" element={<PlatformPage />} />
          </Route>
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </WithSuspense>
  );
}
