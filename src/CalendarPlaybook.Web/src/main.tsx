import { Auth0Provider } from '@auth0/auth0-react';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './fonts.css';
import './index.css';

const root = createRoot(document.getElementById('root')!);

// Auth0 (auth0-spa-js) throws on a non-secure origin (plain http other than localhost),
// which would leave a blank page. Say what is wrong instead.
if (!window.isSecureContext) {
  const httpsUrl = window.location.href.replace(/^http:/, 'https:');
  root.render(
    <StrictMode>
      <p className="error" role="alert">
        This app must be opened over HTTPS: <a href={httpsUrl}>{httpsUrl}</a>
      </p>
    </StrictMode>,
  );
} else {
  root.render(
    <StrictMode>
      <Auth0Provider
        domain={import.meta.env.VITE_AUTH0_DOMAIN}
        clientId={import.meta.env.VITE_AUTH0_CLIENT_ID}
        authorizationParams={{
          redirect_uri: window.location.origin,
          audience: import.meta.env.VITE_AUTH0_AUDIENCE,
        }}
      >
        <App />
      </Auth0Provider>
    </StrictMode>,
  );
}
