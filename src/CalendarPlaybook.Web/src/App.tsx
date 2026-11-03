import { useAuth0 } from '@auth0/auth0-react';
import { useCallback, useMemo } from 'react';
import { createListOccurrences } from './api';
import Calendar from './Calendar';

const apiUrl = import.meta.env.VITE_API_URL ?? '';

export default function App() {
  const { isLoading, isAuthenticated, error, loginWithRedirect, logout, getAccessTokenSilently } = useAuth0();
  const getToken = useCallback(async () => {
    const token = await getAccessTokenSilently();
    if (!token) throw new Error('No access token');
    return token;
  }, [getAccessTokenSilently]);
  const listOccurrences = useMemo(() => createListOccurrences(apiUrl, getToken), [getToken]);

  if (isLoading) return <p className="error" role="status">Loading…</p>;
  if (error) return <p className="error" role="alert">{error.message}</p>;
  if (!isAuthenticated) {
    return (
      <div className="page">
        <header className="site-header">
          <h1>Calendar</h1>
          <button type="button" onClick={() => void loginWithRedirect()}>Sign in</button>
        </header>
      </div>
    );
  }
  return (
    <Calendar
      listOccurrences={listOccurrences}
      onSignOut={() => void logout({ logoutParams: { returnTo: window.location.origin } })}
    />
  );
}
