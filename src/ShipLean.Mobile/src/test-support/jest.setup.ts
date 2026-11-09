// The jest-expo specs' setup (jest.config.js setupFilesAfterEnv): native modules the app binds, faked in memory.
import { restoreFetch } from './fakeApi';

jest.mock('expo-secure-store', () => {
  const { secureValues } = jest.requireActual<typeof import('./storage')>('./storage');
  return {
    __esModule: true,
    getItem: (key: string) => secureValues.get(key) ?? null,
    setItem: (key: string, value: string) => {
      secureValues.set(key, value);
    },
    getItemAsync: async (key: string) => secureValues.get(key) ?? null,
    setItemAsync: async (key: string, value: string) => {
      secureValues.set(key, value);
    },
    deleteItemAsync: async (key: string) => {
      secureValues.delete(key);
    },
  };
});

jest.mock('expo-sqlite/kv-store', () => {
  const { kvValues } = jest.requireActual<typeof import('./storage')>('./storage');
  return {
    __esModule: true,
    default: {
      getItemSync: (key: string) => kvValues.get(key) ?? null,
      setItemSync: (key: string, value: string) => {
        kvValues.set(key, value);
      },
      removeItemSync: (key: string) => kvValues.delete(key),
    },
  };
});

// Specs set what the browser returns: jest.mocked(openAuthSessionAsync).mockResolvedValueOnce({ type: 'success', url }).
jest.mock('expo-web-browser', () => ({
  __esModule: true,
  openAuthSessionAsync: jest.fn(async () => ({ type: 'cancel' })),
  openBrowserAsync: jest.fn(async () => ({ type: 'opened' })),
  dismissBrowser: jest.fn(),
  dismissAuthSession: jest.fn(),
  maybeCompleteAuthSession: jest.fn(() => ({ type: 'failed', message: 'Not in a browser.' })),
}));

jest.mock(
  '@react-native-community/netinfo',
  () => jest.requireActual<typeof import('./netInfo')>('./netInfo').netInfoMock,
);

jest.mock('react-native/Libraries/Utilities/useColorScheme', () => ({
  __esModule: true,
  default: () =>
    jest.requireActual<typeof import('./colorScheme')>('./colorScheme').currentScheme(),
}));

// The app's layout asks for the device's window metrics: a notched phone, as renderScreen uses.
jest.mock('react-native-safe-area-context', () => {
  const mock = jest.requireActual<{ default: Record<string, unknown> }>(
    'react-native-safe-area-context/jest/mock',
  ).default;
  const { phoneMetrics } = jest.requireActual<typeof import('./phone')>('./phone');
  return { ...mock, initialWindowMetrics: phoneMetrics };
});

afterEach(() => {
  restoreFetch();
  jest.clearAllMocks();
});
