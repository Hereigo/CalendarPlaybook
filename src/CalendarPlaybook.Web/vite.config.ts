/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      // Paths relative to the project root. silent-renew.html is the OIDC silent_redirect_uri (src/auth.ts).
      input: {
        main: 'index.html',
        silentRenew: 'silent-renew.html',
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
  },
});
