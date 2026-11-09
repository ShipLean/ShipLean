import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    // Plain Node: the core has no DOM and no DOM test environment.
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
