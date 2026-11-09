import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    // Plain Node for pure modules. Screens and primitives are jest-expo specs (*.spec.tsx, jest.config.js).
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
