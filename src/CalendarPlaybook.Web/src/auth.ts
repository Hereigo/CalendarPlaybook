import { InMemoryWebStorage, WebStorageStateStore, type UserManagerSettings } from 'oidc-client-ts';

/**
 * cidaas sign-in (OIDC Authorization Code + PKCE, spec section 3).
 * Tokens live in memory only (spec section 11); a reload signs in again through the cidaas session.
 */
const resource = import.meta.env.VITE_CIDAAS_RESOURCE;

export const oidcSettings: UserManagerSettings = {
  authority: import.meta.env.VITE_CIDAAS_BASE_URL ?? '',
  client_id: import.meta.env.VITE_CIDAAS_CLIENT_ID ?? '',
  redirect_uri: window.location.origin,
  post_logout_redirect_uri: window.location.origin,
  silent_redirect_uri: `${window.location.origin}/silent-renew.html`,
  response_type: 'code',
  scope: 'openid profile',
  userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
  automaticSilentRenew: true,
  // Optional RFC 8707 resource indicator, when the API audience is a resource URI instead of the client id.
  ...(resource ? { extraQueryParams: { resource } } : {}),
};

/** Drops ?code=&state= from the address bar after the sign-in redirect. */
export function removeSigninParams(): void {
  window.history.replaceState({}, document.title, window.location.pathname);
}
