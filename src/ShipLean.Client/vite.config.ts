/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 3000,
    // The proxy target below must never silently shift ports.
    strictPort: true,
    proxy: {
      // Kestrel's port, from src/ShipLean.WebApi/Properties/launchSettings.json.
      '/api': { target: 'http://localhost:5113', changeOrigin: true },
    },
  },
  build: {
    // Straight into the API project: ASP.NET serves what Vite emits, no copy step.
    outDir: '../ShipLean.WebApi/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    // *.spec.tsx are Playwright component tests (pnpm run test-ct), not vitest.
    include: ['src/**/*.test.{ts,tsx}'],
  },
});
