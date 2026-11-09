import eslint from '@eslint/js';
import tseslint from 'typescript-eslint';

// The core is shared by the web SPA and the phone app, so it may not reach for either one's views or browser globals.
const platformNeutral = 'The core is platform-neutral: views and hooks stay in the apps.';
const notInReactNative = 'Not in React Native: go through platform().';

const viewImports = {
  paths: ['react', 'react-dom', 'react-router-dom', '@tanstack/react-query', 'mermaid'].map(
    (name) => ({ name, message: platformNeutral }),
  ),
  patterns: [
    {
      // paths match exact names only, so subpaths such as react/jsx-runtime are caught here.
      group: [
        'react/*',
        'react-dom/*',
        'react-router-dom/*',
        '@tanstack/react-query/*',
        '@dnd-kit/*',
        '@uiw/*',
        'mermaid/*',
      ],
      message: platformNeutral,
    },
    {
      group: [
        '**/ShipLean.Client',
        '**/ShipLean.Client/**',
        'shiplean-client',
        'shiplean-client/*',
      ],
      message: 'The core never imports from an app: the app passes it what it needs.',
    },
  ],
};
const testSupport = {
  group: ['**/test-support', '**/test-support/**'],
  message: 'src/test-support/ is for *.test.ts files only.',
};

// A declared global passes both tsc and no-restricted-globals, so declaring one is refused outright.
const declaredGlobals = [
  {
    selector: 'VariableDeclaration[declare=true]',
    message: `Declaring a global hides a host dependency. ${notInReactNative}`,
  },
  {
    selector: 'TSModuleDeclaration[kind="global"]',
    message: `Declaring a global hides a host dependency. ${notInReactNative}`,
  },
];

const browserGlobals = [
  'window',
  'document',
  'localStorage',
  'sessionStorage',
  'navigator',
  'location',
  'self',
  'addEventListener',
  'removeEventListener',
  'dispatchEvent',
];
const restrictedGlobals = (names) => names.map((name) => ({ name, message: notInReactNative }));

export default tseslint.config(
  { ignores: ['node_modules'] },
  eslint.configs.recommended,
  tseslint.configs.recommended,
  {
    rules: {
      'no-restricted-imports': ['error', viewImports],
      'no-restricted-globals': ['error', ...restrictedGlobals(browserGlobals)],
    },
  },
  {
    files: ['src/**/*.ts'],
    ignores: ['src/**/*.test.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        { ...viewImports, patterns: [...viewImports.patterns, testSupport] },
      ],
      'no-restricted-syntax': ['error', ...declaredGlobals],
    },
  },
);
