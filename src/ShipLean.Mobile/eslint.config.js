// CommonJS like the rest of this package's config (Expo's Babel and Metro configs are CommonJS).
const eslint = require('@eslint/js');
const reactHooks = require('eslint-plugin-react-hooks');
const tseslint = require('typescript-eslint');
const { noClientDates } = require('./eslint/dates.js');
const datesAllowlist = require('./eslint/dates-allowlist.json');

const shipleanPlugin = { meta: { name: 'shiplean' }, rules: { 'no-client-dates': noClientDates } };

const testSupport = {
  group: ['**/test-support', '**/test-support/**'],
  message: 'src/test-support/ is for *.test.ts and *.spec.tsx files only.',
};

module.exports = tseslint.config(
  // Expo's caches, generated route types and the bundle check output.
  {
    ignores: [
      'node_modules',
      '.expo',
      'dist',
      'expo-env.d.ts',
      'ios',
      'android',
      'tools/voice-eval/.venv/',
      'tools/voice-eval/out/',
    ],
  },
  eslint.configs.recommended,
  tseslint.configs.recommended,
  reactHooks.configs.flat['recommended-latest'],
  {
    // The config files (and the eslint/ rules they load) are CommonJS and run in Node.
    files: ['*.js', 'eslint/*.js'],
    languageOptions: {
      sourceType: 'commonjs',
      globals: { module: 'writable', require: 'readonly', __dirname: 'readonly' },
    },
    rules: { '@typescript-eslint/no-require-imports': 'off' },
  },
  {
    files: ['**/*.ts', '**/*.tsx'],
    ignores: ['**/*.test.ts', '**/*.spec.tsx'],
    rules: { 'no-restricted-imports': ['error', { patterns: [testSupport] }] },
  },
  // Dates are server labels (SHIP-1702 D7): no Date objects, Intl date formats or date libraries outside tests.
  {
    files: ['src/**/*.{ts,tsx}', 'app/**/*.{ts,tsx}', 'modules/**/*.{ts,tsx}'],
    ignores: ['**/*.test.ts', '**/*.test.tsx', '**/*.spec.tsx', '**/test-support/**'],
    plugins: { shiplean: shipleanPlugin },
    rules: {
      'shiplean/no-client-dates': [
        'error',
        {
          allowlist: datesAllowlist,
          exceptions: [
            // The one clock the actions read (D7).
            { file: 'src/actions/clock.ts', allow: ['new Date'] },
            // Mic tuning's "tested today" label is device-only data (D10).
            {
              file: 'src/screens/micTuning/input/inputLabels.ts',
              allow: ['new Date', 'getTimezoneOffset'],
            },
          ],
        },
      ],
    },
  },
);
