import { UserManager } from 'oidc-client-ts';
import { oidcSettings } from './auth';

// Runs in the hidden iframe of a silent token renewal and hands the result back to the app window.
void new UserManager(oidcSettings).signinSilentCallback();
