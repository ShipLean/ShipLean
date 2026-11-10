import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The command-model eval: pure Node, run on a workstation against downloaded models, never in the app's suites.
// Run from src/ShipLean.Mobile: pnpm exec vitest run --config tools/voice-eval/vitest.config.ts
export default defineConfig({
  test: {
    root: fileURLToPath(new URL('../..', import.meta.url)),
    environment: 'node',
    include: ['tools/voice-eval/**/*.test.ts'],
  },
});
