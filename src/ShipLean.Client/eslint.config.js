import eslint from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import tseslint from 'typescript-eslint';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { breakpointsPlugin } from './eslint/breakpoints.js';
import { noClientDates } from './eslint/dates.js';

// One plugin object for every shiplean/* rule: flat config refuses two different objects under one plugin name.
const shipleanPlugin = {
  meta: { name: 'shiplean' },
  rules: { ...breakpointsPlugin.rules, 'no-client-dates': noClientDates },
};
const datesAllowlist = JSON.parse(
  readFileSync(path.join(import.meta.dirname, 'eslint', 'dates-allowlist.json'), 'utf8'),
);

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
    plugins: { shiplean: shipleanPlugin },
    rules: { 'shiplean/no-breakpoint-variants': 'error' },
  },
  // Dates are server labels (SHIP-1702 D7): no Date objects, Intl date formats or date libraries outside tests.
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/**/*.test.{ts,tsx}', 'src/**/*.spec.{ts,tsx}', 'src/**/ct/**'],
    plugins: { shiplean: shipleanPlugin },
    rules: {
      'shiplean/no-client-dates': [
        'error',
        {
          allowlist: datesAllowlist,
          exceptions: [
            // Reads the browser's zone once, for the first-sign-in capture (D2).
            { file: 'src/features/shell/timeZone/browserZone.ts', allow: ['Intl.DateTimeFormat'] },
          ],
        },
      ],
    },
  },
);
