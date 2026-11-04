import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AuthProvider } from 'react-oidc-context';
import App from './App';
import { oidcSettings, removeSigninParams } from './auth';
import './fonts.css';
import './index.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider {...oidcSettings} onSigninCallback={removeSigninParams}>
      <App />
    </AuthProvider>
  </StrictMode>,
);
