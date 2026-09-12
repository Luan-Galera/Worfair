import { Outlet } from 'react-router-dom';
import { TopNav } from '../components/layout/TopNav';
import { SideNav } from '../components/layout/SideNav';

export function AppLayout() {
  return (
    <div className="app-shell">
      <TopNav />
      <div className="container py-4">
        <div className="row g-4">
          <div className="col-12 col-lg-3">
            <SideNav />
          </div>
          <div className="col-12 col-lg-9">
            <Outlet />
          </div>
        </div>
      </div>
    </div>
  );
}
