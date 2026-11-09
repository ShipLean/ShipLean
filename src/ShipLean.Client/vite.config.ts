/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { viteBuildStatsPlugin } from 'agoda-devfeedback-vite2';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    // Build and dev-server timings to VITE_ENDPOINT (devex-telemetry).
    // Not under vitest: it runs the plugin once per vite environment and posts each test run as a vite build.
    ...(process.env.VITEST ? [] : [viteBuildStatsPlugin()]),
  ],
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
    // The web platform for @shiplean/core, as main.tsx configures it.
    setupFiles: ['src/vitestSetup.ts'],
    // Test runs post to VITEST_METRICS_ENDPOINT (devex-telemetry); unset, the post fails silently.
    reporters: ['default', 'agoda-test-metrics/vitest'],
  },
});
