/** @type {import('jest').Config} */
module.exports = {
  preset: 'jest-expo',
  // Screens and primitives only. Pure modules are vitest (*.test.ts, vitest.config.ts); specs never go under app/,
  // because expo-router treats every file there as a route.
  testMatch: ['<rootDir>/src/**/*.spec.tsx'],
  // Busy CI runners blow the 5 s default on screen specs.
  testTimeout: 15000,
  // Native modules faked in memory: secure store, kv-store, web browser, NetInfo, colour scheme, safe area.
  setupFilesAfterEnv: ['<rootDir>/src/test-support/jest.setup.ts'],
};
