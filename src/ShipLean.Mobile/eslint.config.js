// CommonJS like the rest of this package's config (Expo's Babel and Metro configs are CommonJS).
const eslint = require('@eslint/js');
const reactHooks = require('eslint-plugin-react-hooks');
const tseslint = require('typescript-eslint');

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
    // The config files are CommonJS and run in Node.
    files: ['*.js'],
    languageOptions: {
      sourceType: 'commonjs',
      globals: { module: 'writable', require: 'readonly' },
    },
    rules: { '@typescript-eslint/no-require-imports': 'off' },
  },
  {
    files: ['**/*.ts', '**/*.tsx'],
    ignores: ['**/*.test.ts', '**/*.spec.tsx'],
    rules: { 'no-restricted-imports': ['error', { patterns: [testSupport] }] },
  },
);
