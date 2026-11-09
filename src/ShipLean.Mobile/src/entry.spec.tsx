// The core reads crypto.randomUUID when its client module loads, and Hermes has none, so index.ts must load the
// polyfill before expo-router's entry pulls in the app (and with it @shiplean/core). This runs the real index.ts on
// a runtime with no crypto at all: expo-router's entry is replaced by a module that loads the core client.
jest.mock('expo-crypto', () => ({
  randomUUID: () => '00000000-0000-4000-8000-00000000c0de',
  getRandomValues: <T,>(array: T) => array,
}));
jest.mock('expo-router/entry', () => ({
  clientId: jest.requireActual<typeof import('@shiplean/core/api/client')>(
    '@shiplean/core/api/client',
  ).clientId,
}));

test('entry_WhenTheRuntimeHasNoCrypto_ShouldPolyfillItBeforeTheCoreLoads', () => {
  const original = Object.getOwnPropertyDescriptor(globalThis, 'crypto');
  Object.defineProperty(globalThis, 'crypto', {
    value: undefined,
    configurable: true,
    writable: true,
  });
  try {
    let clientId: unknown;
    jest.isolateModules(() => {
      require('../index');
      clientId = (require('expo-router/entry') as { clientId: string }).clientId;
    });

    expect(clientId).toBe('00000000-0000-4000-8000-00000000c0de');
  } finally {
    if (original) Object.defineProperty(globalThis, 'crypto', original);
  }
});
