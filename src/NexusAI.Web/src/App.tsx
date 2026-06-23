import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider, useAuth } from './auth/AuthProvider';
import { AdminPage } from './pages/AdminPage';
import { ChatPage } from './pages/ChatPage';
import './App.css';

function AppContent() {
  const { isReady, isAuthenticated } = useAuth();

  if (!isReady) {
    return <div className="loading-screen">Connecting to Keycloak…</div>;
  }

  if (!isAuthenticated) {
    return <div className="loading-screen">Redirecting to sign in…</div>;
  }

  return (
    <Routes>
      <Route path="/" element={<ChatPage />} />
      <Route path="/admin" element={<AdminPage />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <AppContent />
      </BrowserRouter>
    </AuthProvider>
  );
}

export default App;
