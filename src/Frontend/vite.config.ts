import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// The dev server mirrors the two paths nginx serves in the container, so the browser
// code calls the same URLs in both. Any `VITE_*_API_URL` would exist only in one
// of the two environments and become the difference that breaks the container.
const devProxy = {
  '/api/cashflow': {
    target: process.env.CASHFLOW_API_URL ?? 'http://localhost:5001',
    changeOrigin: true,
    rewrite: (path: string) => path.replace(/^\/api\/cashflow/, ''),
  },
  '/api/consolidation': {
    target: process.env.CONSOLIDATION_API_URL ?? 'http://localhost:5002',
    changeOrigin: true,
    rewrite: (path: string) => path.replace(/^\/api\/consolidation/, ''),
  },
};

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: devProxy,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
    css: false,
  },
});
