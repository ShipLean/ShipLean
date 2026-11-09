import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    // Plain Node: the core has no DOM. A file that needs one says so with a @vitest-environment comment.
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
