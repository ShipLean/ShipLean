import eslint from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import tseslint from 'typescript-eslint';
import { breakpointsPlugin } from './eslint/breakpoints.js';

export default tseslint.config(
  {
    ignores: [
      '../ShipLean.WebApi/wwwroot',
      'playwright/.cache',
      'playwright-report',
      'test-results',
    ],
  },
  eslint.configs.recommended,
  tseslint.configs.recommended,
  // eslint-plugin-react-hooks@7's top-level `recommended-latest` still
  // exports `plugins` as a legacy string array, which ESLint 10's flat
  // config rejects; the properly-shaped flat variant lives under `.flat`.
  reactHooks.configs.flat['recommended-latest'],
  // One touch breakpoint (K9, docs/mobile.md): width variants at or below 900px are banned outside the allowlist.
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/**/*.test.{ts,tsx}'],
    plugins: { shiplean: breakpointsPlugin },
    rules: { 'shiplean/no-breakpoint-variants': 'error' },
  },
);
