import { Outlet } from 'react-router-dom';
import { TopNav } from '../components/layout/TopNav';

export function PublicLayout() {
  return (
    <div className="app-shell">
      <TopNav />
      <main className="container py-4">
        <Outlet />
      </main>
      <footer className="border-top py-3 text-center text-muted small">
        Worfair · marketplace de trabalhos e vagas · taxa transparente de 15%
      </footer>
    </div>
  );
}
