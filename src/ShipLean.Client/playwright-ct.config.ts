import tailwindcss from '@tailwindcss/vite';
import { defineConfig, devices } from '@playwright/experimental-ct-react';

export default defineConfig({
  testDir: './src',
  testMatch: '**/*.spec.tsx',
  timeout: 30_000,
  reporter: [['list'], ['html', { open: 'never' }]],
  expect: {
    // Baselines are generated inside the same Docker image CI runs in, so
    // renders are near-deterministic and the budget can be a small absolute
    // one. The old 2% ratio was so lax that a full-page restyle of the
    // sparser (mostly-background) pages still passed against stale baselines.
    toHaveScreenshot: { maxDiffPixels: 250 },
  },
  // Screenshots are only compared in CI (or a run inside the same image with CI set): another machine's system fonts
  // shift the side menu's glyphs and every page shot would fail locally.
  ignoreSnapshots: !process.env.CI,
  use: {
    ...devices['Desktop Chrome'],
    // Playwright CT bundles components with its own internal Vite instance —
    // it does not read plugins from vite.config.ts, so Tailwind has to be
    // registered here too or component screenshots render unstyled.
    ctViteConfig: {
      plugins: [tailwindcss()],
    },
  },
});
