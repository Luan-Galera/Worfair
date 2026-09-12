import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { AccessProvider } from './access/AccessContext';
import { AppRouter } from './routing/AppRouter';

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AccessProvider>
          <AppRouter />
        </AccessProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
