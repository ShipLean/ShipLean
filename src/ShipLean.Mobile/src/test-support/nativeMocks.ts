// The settable state behind jest.setup.ts's mocks of the native voice modules (llama.rn, react-native-sherpa-onnx,
// expo-audio, expo-file-system, expo-device). Pure: no React Native, so vitest can load it too. Reset after each test.

export interface MicPermissionAnswer {
  status: 'granted' | 'undetermined' | 'denied';
  granted: boolean;
  canAskAgain: boolean;
}

// One call on an expo-audio stream, in order: the stream's options when created, then start and stop.
export type AudioStreamCall =
  | { call: 'create'; sampleRate: number; channels: number; encoding: string }
  | { call: 'start' }
  | { call: 'stop' };

export type DownloadOutcome = { status: number } | { error: string };

export interface PendingDownload {
  url: string;
  fileUri: string;
  paused: boolean;
  // Reports progress to the download's callback.
  progress(written: number, expected: number): void;
  // Ends the download: the file lands in the file map with its body (200), or the download rejects.
  finish(outcome?: DownloadOutcome, body?: string): void;
}

interface NativeState {
  mic: MicPermissionAnswer;
  micRequests: number;
  streamCalls: AudioStreamCall[];
  totalMemory: number | null;
  deviceName: string | null;
  modelName: string | null;
  files: Map<string, string>;
  downloads: PendingDownload[];
  llamaInits: { model: string }[];
  llamaCompletion: string;
  sttInits: unknown[];
  sttText: string;
}

function initial(): NativeState {
  return {
    mic: { status: 'undetermined', granted: false, canAskAgain: true },
    micRequests: 0,
    streamCalls: [],
    totalMemory: 12 * 1024 ** 3,
    deviceName: 'Test phone',
    modelName: 'Pixel 9',
    files: new Map(),
    downloads: [],
    llamaInits: [],
    llamaCompletion: '[]',
    sttInits: [],
    sttText: '',
  };
}

export const native: NativeState = initial();

export function resetNativeMocks(): void {
  Object.assign(native, initial());
}

export function setMicPermission(answer: MicPermissionAnswer): void {
  native.mic = answer;
}

export function setTotalMemory(bytes: number | null): void {
  native.totalMemory = bytes;
}

type DownloadResult = { uri: string; status: number } | undefined;
type SavedDownload = { url: string; fileUri: string; options: object; resumeData: string };

// expo-file-system/legacy's DownloadResumable over the file map. The download stays pending until the spec calls
// finish on its entry in native.downloads; pauseAsync settles the running call with undefined, as the real one does.
export function resumableDownload(
  url: string,
  fileUri: string,
  callback?: (p: { totalBytesWritten: number; totalBytesExpectedToWrite: number }) => void,
): {
  downloadAsync(): Promise<DownloadResult>;
  resumeAsync(): Promise<DownloadResult>;
  pauseAsync(): Promise<SavedDownload>;
  savable(): SavedDownload;
} {
  let settle: (result: DownloadResult) => void = () => undefined;
  let fail: (e: Error) => void = () => undefined;
  const run = () =>
    new Promise<DownloadResult>((resolve, reject) => {
      settle = resolve;
      fail = reject;
    });
  const saved = (): SavedDownload => ({ url, fileUri, options: {}, resumeData: 'resume' });
  const entry: PendingDownload = {
    url,
    fileUri,
    paused: false,
    progress: (written, expected) =>
      callback?.({ totalBytesWritten: written, totalBytesExpectedToWrite: expected }),
    finish: (outcome = { status: 200 }, body = '') => {
      if ('error' in outcome) {
        fail(new Error(outcome.error));
        return;
      }
      if (outcome.status === 200) native.files.set(fileUri, body);
      settle({ uri: fileUri, status: outcome.status });
    },
  };
  native.downloads.push(entry);
  return {
    downloadAsync: run,
    resumeAsync: () => {
      entry.paused = false;
      return run();
    },
    pauseAsync: async () => {
      entry.paused = true;
      settle(undefined);
      return saved();
    },
    savable: saved,
  };
}
