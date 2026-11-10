// The jest-expo specs' setup (jest.config.js setupFilesAfterEnv): native modules the app binds, faked in memory.
import { queryClient } from '../queryClient';
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

// renderApp's screens read through the app's one query cache (renderScreen's own clients never collect). Each query
// whose screen unmounts schedules its garbage collection on a real timer (5 minutes; 7 days for the Inbox), so the
// last spec of a file left jest unable to exit (SHIP-1328). Dropping the cache once the file is done clears them.
afterAll(() => queryClient.clear());

// Native voice modules (T0v): llama.rn, react-native-sherpa-onnx, expo-audio, expo-file-system and expo-device. Their
// settable state is in nativeMocks.ts; the local model-files module's mock is in mocks/modelFiles.ts.
import './mocks/modelFiles';
import { resetNativeMocks, type AudioBufferEvent } from './nativeMocks';

type NativeMocks = typeof import('./nativeMocks');

jest.mock('llama.rn', () => {
  const { native } = jest.requireActual<NativeMocks>('./nativeMocks');
  return {
    __esModule: true,
    initLlama: jest.fn(async (params: { model: string }) => {
      native.llamaInits.push({ model: params.model });
      return {
        completion: jest.fn(async () => ({ text: native.llamaCompletion })),
        stopCompletion: jest.fn(async () => undefined),
        release: jest.fn(async () => undefined),
      };
    }),
    releaseAllLlama: jest.fn(async () => undefined),
    loadLlamaModelInfo: jest.fn(async () => ({})),
  };
});

jest.mock('react-native-sherpa-onnx/stt', () => {
  const { native } = jest.requireActual<NativeMocks>('./nativeMocks');
  return {
    __esModule: true,
    createSTT: jest.fn(async (options: unknown) => {
      native.sttInits.push(options);
      return {
        instanceId: `stt-${native.sttInits.length}`,
        transcribeFile: jest.fn(async () => ({ text: native.sttText, tokens: [], timestamps: [] })),
        transcribeSamples: jest.fn(async () => ({
          text: native.sttText,
          tokens: [],
          timestamps: [],
        })),
        setConfig: jest.fn(async () => undefined),
        destroy: jest.fn(async () => undefined),
      };
    }),
  };
});

jest.mock('expo-audio', () => {
  const { native } = jest.requireActual<NativeMocks>('./nativeMocks');
  const permission = async () => ({ ...native.mic, expires: 'never' });
  class AudioStream {
    readonly sampleRate: number;
    readonly channels: number;
    isStreaming = false;
    constructor(options: { sampleRate: number; channels: number; encoding: string }) {
      this.sampleRate = options.sampleRate;
      this.channels = options.channels;
      native.streamCalls.push({ call: 'create', ...options });
    }
    async start() {
      this.isStreaming = true;
      native.streamCalls.push({ call: 'start' });
    }
    async stop() {
      this.isStreaming = false;
      native.streamCalls.push({ call: 'stop' });
    }
    addListener(event: string, listener: (buffer: AudioBufferEvent) => void) {
      if (event === 'audioStreamBuffer') native.audioListeners.push(listener);
      return {
        remove: () => {
          const at = native.audioListeners.indexOf(listener);
          if (at >= 0) native.audioListeners.splice(at, 1);
        },
      };
    }
    release() {}
  }
  const getRecordingPermissionsAsync = jest.fn(permission);
  const requestRecordingPermissionsAsync = jest.fn(async () => {
    native.micRequests += 1;
    return permission();
  });
  return {
    __esModule: true,
    AudioModule: { AudioStream, getRecordingPermissionsAsync, requestRecordingPermissionsAsync },
    useAudioStream: (
      options: { sampleRate?: number; channels?: number; encoding?: string } = {},
    ) => {
      // One stream per mounted hook, as the real hook keeps one: a re-render must not log another 'create'.
      const { useRef } = jest.requireActual<typeof import('react')>('react');
      const held = useRef<AudioStream | null>(null);
      held.current ??= new AudioStream({
        sampleRate: options.sampleRate ?? 16000,
        channels: options.channels ?? 1,
        encoding: options.encoding ?? 'int16',
      });
      return { stream: held.current, isStreaming: false };
    },
    getRecordingPermissionsAsync,
    requestRecordingPermissionsAsync,
    setAudioModeAsync: jest.fn(async () => undefined),
    setIsAudioActiveAsync: jest.fn(async () => undefined),
  };
});

// The file map and resumable downloads, for both the current and the legacy API.
jest.mock('expo-file-system/legacy', () => {
  const { native, resumableDownload } = jest.requireActual<NativeMocks>('./nativeMocks');
  return {
    __esModule: true,
    documentDirectory: 'file:///document/',
    cacheDirectory: 'file:///cache/',
    getFreeDiskStorageAsync: jest.fn(async () => 64 * 1024 ** 3),
    getInfoAsync: jest.fn(async (uri: string) => {
      const body = native.files.get(uri);
      return body === undefined ? { exists: false, uri } : { exists: true, uri, size: body.length };
    }),
    readAsStringAsync: jest.fn(async (uri: string) => {
      const body = native.files.get(uri);
      if (body === undefined) throw new Error(`No file at ${uri}`);
      return body;
    }),
    writeAsStringAsync: jest.fn(async (uri: string, body: string) => {
      native.files.set(uri, body);
    }),
    deleteAsync: jest.fn(async (uri: string) => {
      native.files.delete(uri);
    }),
    makeDirectoryAsync: jest.fn(async () => undefined),
    createDownloadResumable: jest.fn(
      (
        url: string,
        fileUri: string,
        _options?: unknown,
        callback?: (p: { totalBytesWritten: number; totalBytesExpectedToWrite: number }) => void,
      ) => resumableDownload(url, fileUri, callback),
    ),
  };
});

jest.mock('expo-file-system', () => {
  const { native } = jest.requireActual<NativeMocks>('./nativeMocks');
  const join = (parts: unknown[]) =>
    parts
      .map((p) => (typeof p === 'string' ? p : (p as { uri: string }).uri))
      .join('/')
      .replace(/([^:/])\/+/g, '$1/');
  class File {
    readonly uri: string;
    constructor(...parts: unknown[]) {
      this.uri = join(parts);
    }
    get name() {
      return this.uri.slice(this.uri.lastIndexOf('/') + 1);
    }
    get exists() {
      return native.files.has(this.uri);
    }
    get size() {
      return native.files.get(this.uri)?.length ?? 0;
    }
    text() {
      return Promise.resolve(native.files.get(this.uri) ?? '');
    }
    write(body: string) {
      native.files.set(this.uri, body);
    }
    delete() {
      native.files.delete(this.uri);
    }
  }
  class Directory {
    readonly uri: string;
    constructor(...parts: unknown[]) {
      this.uri = join(parts);
    }
    get exists() {
      return true;
    }
    create() {}
    // The files directly under this directory.
    list() {
      return [...native.files.keys()]
        .filter(
          (uri) => uri.startsWith(`${this.uri}/`) && !uri.slice(this.uri.length + 1).includes('/'),
        )
        .map((uri) => new File(uri));
    }
  }
  return {
    __esModule: true,
    File,
    Directory,
    Paths: {
      document: new Directory('file:///document'),
      cache: new Directory('file:///cache'),
      get availableDiskSpace() {
        return 64 * 1024 ** 3;
      },
    },
  };
});

// SignInScreen reads deviceName and modelName; deviceMemory.ts reads totalMemory.
jest.mock('expo-device', () => {
  const { native } = jest.requireActual<NativeMocks>('./nativeMocks');
  const device = { __esModule: true };
  for (const key of ['totalMemory', 'deviceName', 'modelName'] as const) {
    Object.defineProperty(device, key, { enumerable: true, get: () => native[key] });
  }
  return device;
});

afterEach(() => {
  resetNativeMocks();
});
