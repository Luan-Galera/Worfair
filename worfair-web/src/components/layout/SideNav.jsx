import { NavLink } from 'react-router-dom';
import { useAccess } from '../../access/useAccess';

// Menu filtrado pelo espelho do /me (o backend valida tudo de novo).
// Painel e Faturas são do super admin; o resto é do espaço do usuário.
const NAV = [
  { to: '/painel', label: 'Painel', icon: 'bi-grid', permission: 'platform.tenants.manage' },
  { to: '/financeiro', label: 'Faturas', icon: 'bi-wallet2', permission: 'platform.tenants.manage' },
  { to: '/plataforma', label: 'Plataforma', icon: 'bi-shield-check', permission: 'platform.tenants.manage' },
  { to: '/contratar', label: 'Contratar / publicar', icon: 'bi-plus-circle', permission: 'financial.invoice.issue' },
  { to: '/empresa', label: 'Empresa', icon: 'bi-building', permission: 'tenants.settings.read' },
  { to: '/equipe', label: 'Equipe e cargos', icon: 'bi-people', permission: 'tenants.members.manage' },
  { to: '/vagas', label: 'Vagas', icon: 'bi-briefcase', permission: null },
  { to: '/trabalhos', label: 'Trabalhos', icon: 'bi-kanban', permission: null },
  { to: '/propostas', label: 'Propostas', icon: 'bi-send', permission: null },
  { to: '/mensagens', label: 'Mensagens', icon: 'bi-chat-dots', permission: null },
  { to: '/notificacoes', label: 'Notificações', icon: 'bi-bell', permission: null },
];

export function SideNav() {
  const { can } = useAccess();
  const items = NAV.filter((i) => !i.permission || can(i.permission));
  return (
    <div className="list-group">
      {items.map((i) => (
        <NavLink key={i.to} to={i.to} className="list-group-item list-group-item-action">
          <i className={`bi ${i.icon} me-2`} aria-hidden="true" />
          {i.label}
        </NavLink>
      ))}
    </div>
  );
}
