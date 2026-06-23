import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { initKeycloak, keycloak } from './keycloak';

type AuthContextValue = {
  isReady: boolean;
  isAuthenticated: boolean;
  username: string | null;
  roles: string[];
  isAdmin: boolean;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

function readRoles(): string[] {
  const token = keycloak.tokenParsed as { roles?: string | string[] } | undefined;
  if (!token?.roles) {
    return [];
  }

  return Array.isArray(token.roles) ? token.roles : [token.roles];
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isReady, setIsReady] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [roles, setRoles] = useState<string[]>([]);

  useEffect(() => {
    void initKeycloak()
      .then((authenticated) => {
        setIsAuthenticated(authenticated);
        setRoles(readRoles());
        setIsReady(true);
      })
      .catch(() => setIsReady(true));
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      isReady,
      isAuthenticated,
      username: keycloak.tokenParsed?.preferred_username ?? null,
      roles,
      isAdmin: roles.includes('admin'),
      logout: () => keycloak.logout({ redirectUri: window.location.origin }),
    }),
    [isAuthenticated, isReady, roles],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
}
