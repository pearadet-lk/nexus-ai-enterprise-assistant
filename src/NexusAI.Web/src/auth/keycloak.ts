import Keycloak from 'keycloak-js';

const keycloakConfig = {
  url: import.meta.env.VITE_KEYCLOAK_URL ?? 'http://localhost:8080',
  realm: import.meta.env.VITE_KEYCLOAK_REALM ?? 'nexusai',
  clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID ?? 'nexusai-web',
};

export const keycloak = new Keycloak(keycloakConfig);

export async function initKeycloak(): Promise<boolean> {
  const authenticated = await keycloak.init({
    onLoad: 'login-required',
    checkLoginIframe: false,
    pkceMethod: 'S256',
  });

  if (authenticated) {
  keycloak.onTokenExpired = () => {
    void keycloak.updateToken(30);
  };
  }

  return authenticated;
}

export function getAccessToken(): string | undefined {
  return keycloak.token;
}
