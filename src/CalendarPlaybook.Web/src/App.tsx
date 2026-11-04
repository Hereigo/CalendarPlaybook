import { useCallback, useMemo } from 'react';
import { useAuth } from 'react-oidc-context';
import { createApi } from './api';
import Calendar from './Calendar';

const apiUrl = import.meta.env.VITE_API_URL ?? '';

export default function App() {
  const { isLoading, isAuthenticated, error, user, signinRedirect, signinSilent, signoutRedirect } = useAuth();
  const getToken = useCallback(async () => {
    let current = user;
    if (!current || current.expired) current = await signinSilent();
    const token = current?.access_token;
    if (!token) throw new Error('No access token');
    return token;
  }, [user, signinSilent]);
  const api = useMemo(() => createApi(apiUrl, getToken), [getToken]);

  if (isLoading) return <p className="error" role="status">Loading…</p>;
  if (error) return <p className="error" role="alert">{error.message}</p>;
  if (!isAuthenticated) {
    return (
      <div className="page">
        <header className="site-header">
          <h1>Calendar</h1>
          <button type="button" onClick={() => void signinRedirect()}>Sign in</button>
        </header>
      </div>
    );
  }
  return <Calendar api={api} onSignOut={() => void signoutRedirect()} />;
}
