import type { DownloadPauseState } from 'expo-file-system';
import { Platform } from 'react-native';
import { excludeFromBackup } from '../../../modules/model-files';
import { kvValues } from '../../test-support/storage';
import { ExpoModelFiles } from './modelFiles';
import type { ModelFile } from './ports';

// expo-file-system's download task and disk, driven by the test. The shared mock (jest.setup.ts) has neither
// File.createDownloadTask nor the paths under Library, so this spec mocks the API it uses.
interface MockTask {
  url: string;
  dest: string;
  restoredFrom: DownloadPauseState | null;
  state: 'idle' | 'active' | 'paused' | 'completed' | 'error';
  calls: string[];
  options: { onProgress?: (p: { bytesWritten: number; totalBytes: number }) => void };
  progress(bytes: number): void;
  finish(bytes: number): void;
  fail(message: string): void;
}

const mockFs = {
  disk: new Map<string, number>(),
  tasks: [] as MockTask[],
  digests: [] as string[],
  free: 9_000_000_000,
  badSavable: false,
};

jest.mock('expo-file-system', () => {
  const join = (parts: unknown[]) => {
    const raw = parts
      .map((p) => (typeof p === 'string' ? p : (p as { uri: string }).uri))
      .join('/');
    const out: string[] = [];
    for (const seg of raw.replace(/^file:\/\//, '').split('/')) {
      if (seg === '' || seg === '.') continue;
      if (seg === '..') out.pop();
      else out.push(seg);
    }
    return `file:///${out.join('/')}`;
  };
  class Directory {
    readonly uri: string;
    constructor(...parts: unknown[]) {
      this.uri = join(parts);
    }
    get exists() {
      return mockFs.disk.has(this.uri);
    }
    create() {
      mockFs.disk.set(this.uri, 0);
    }
  }
  class File {
    readonly uri: string;
    constructor(...parts: unknown[]) {
      this.uri = join(parts);
    }
    get exists() {
      return mockFs.disk.has(this.uri);
    }
    get size() {
      return mockFs.disk.get(this.uri) ?? 0;
    }
    delete() {
      mockFs.disk.delete(this.uri);
    }
    rename(name: string) {
      const to = `${this.uri.slice(0, this.uri.lastIndexOf('/'))}/${name}`;
      mockFs.disk.set(to, this.size);
      mockFs.disk.delete(this.uri);
    }
    async digest(algorithm: string) {
      mockFs.digests.push(`${algorithm} ${this.uri}`);
      return `digest-of-${this.uri}`;
    }
    static createDownloadTask(url: string, dest: File, options: MockTask['options']) {
      return new DownloadTask(url, dest.uri, options, null);
    }
  }
  class DownloadTask implements MockTask {
    state: MockTask['state'];
    calls: string[] = [];
    private settle: ((file: File | null) => void) | null = null;
    private reject: ((error: Error) => void) | null = null;
    readonly url: string;
    readonly dest: string;
    readonly options: MockTask['options'];
    readonly restoredFrom: DownloadPauseState | null;
    constructor(
      url: string,
      dest: string,
      options: MockTask['options'],
      restoredFrom: DownloadPauseState | null,
    ) {
      this.url = url;
      this.dest = dest;
      this.options = options;
      this.restoredFrom = restoredFrom;
      this.state = restoredFrom === null ? 'idle' : 'paused';
      mockFs.tasks.push(this);
    }
    static fromSavable(state: DownloadPauseState, options: MockTask['options']) {
      if (mockFs.badSavable) throw new Error('Cannot restore task');
      return new DownloadTask(state.url, state.fileUri, options, state);
    }
    private run(call: string) {
      this.calls.push(call);
      this.state = 'active';
      return new Promise<File | null>((resolve, reject) => {
        this.settle = resolve;
        this.reject = reject;
      });
    }
    downloadAsync() {
      return this.run('download');
    }
    resumeAsync() {
      return this.run('resume');
    }
    pause() {
      this.calls.push('pause');
      this.state = 'paused';
      this.settle?.(null);
    }
    savable(): DownloadPauseState {
      return { url: this.url, fileUri: this.dest, isDirectory: false, resumeData: 'resume-data' };
    }
    async acknowledgeBackgroundCompletionAsync() {
      this.calls.push('acknowledge');
    }
    // The test's side: Android writes into the destination as it goes.
    progress(bytes: number) {
      mockFs.disk.set(this.dest, bytes);
      this.options.onProgress?.({ bytesWritten: bytes, totalBytes: -1 });
    }
    finish(bytes: number) {
      this.progress(bytes);
      this.state = 'completed';
      this.settle?.(new File(this.dest));
    }
    fail(message: string) {
      this.state = 'error';
      this.reject?.(new Error(message));
    }
  }
  return {
    __esModule: true,
    File,
    Directory,
    DownloadTask,
    Paths: {
      document: new Directory('file:///app/Documents'),
      get availableDiskSpace() {
        return mockFs.free;
      },
    },
  };
});

const mockNet = { type: 'wifi' };

jest.mock('@react-native-community/netinfo', () => ({
  __esModule: true,
  default: { fetch: async () => ({ type: mockNet.type }) },
}));

// MODEL_FILES has no file names or URLs until the pinned manifest lands: one Parakeet file and one Qwen file.
jest.mock('../manifest', () => {
  const actual = jest.requireActual<typeof import('../manifest')>('../manifest');
  const base = actual.MODEL_FILES[0];
  return {
    ...actual,
    MODEL_FILES: [
      {
        ...base,
        id: 'parakeet-tokens',
        model: 'parakeet',
        fileName: 'tokens.txt',
        url: 'https://example.test/tokens.txt',
        bytes: 1000,
      },
      {
        ...base,
        id: 'qwen3-4b-q4_k_m',
        model: 'qwen3-4b',
        fileName: 'Qwen3-4B-Q4_K_M.gguf',
        url: 'https://example.test/qwen.gguf',
        bytes: 5000,
      },
    ],
  };
});

const mockQwen = { id: 'qwen3-4b-q4_k_m', url: 'https://example.test/qwen.gguf' };
const ROOT = 'file:///app/Library/Application Support/models';
const QWEN_DIR = `${ROOT}/qwen3-4b`;
const QWEN_PART = `${QWEN_DIR}/qwen3-4b-q4_k_m.part`;
const QWEN_FINAL = `${QWEN_DIR}/Qwen3-4B-Q4_K_M.gguf`;

function qwenFile(): ModelFile {
  const file = jest
    .requireMock<typeof import('../manifest')>('../manifest')
    .MODEL_FILES.find((f) => f.id === mockQwen.id);
  if (file === undefined) throw new Error('no Qwen file in the mocked manifest');
  return file;
}

function lastTask(): MockTask {
  const task = mockFs.tasks.at(-1);
  if (task === undefined) throw new Error('no download task');
  return task;
}

// Lets the adapter reach the native task (it awaits NetInfo and the directories first).
async function settle(): Promise<void> {
  for (let i = 0; i < 10; i += 1) await Promise.resolve();
}

function start(files = new ExpoModelFiles(), cellular = false) {
  const progress: number[] = [];
  const task = files.download(qwenFile(), {
    onProgress: (bytes) => progress.push(bytes),
    cellular,
  });
  return { files, task, progress };
}

let android: { restore(): void } | null = null;

afterEach(() => {
  android?.restore();
  android = null;
});

beforeEach(() => {
  mockFs.disk.clear();
  mockFs.tasks.length = 0;
  mockFs.digests.length = 0;
  mockFs.badSavable = false;
  mockNet.type = 'wifi';
  kvValues.clear();
});

describe('modelFilesAdapter', () => {
  it('downloads to a part file and renames it only when complete', async () => {
    const { files, task, progress } = start();
    await settle();
    const native = lastTask();
    expect(native.url).toBe(mockQwen.url);
    expect(native.dest).toBe(QWEN_PART);

    native.progress(2000);
    expect(progress).toEqual([2000]);
    expect(mockFs.disk.has(QWEN_FINAL)).toBe(false);
    await expect(files.bytesOnDisk(mockQwen.id)).resolves.toBe(2000);

    native.finish(5000);
    await expect(task.done).resolves.toBe('complete');
    expect(mockFs.disk.has(QWEN_PART)).toBe(false);
    expect(mockFs.disk.get(QWEN_FINAL)).toBe(5000);
    await expect(files.bytesOnDisk(mockQwen.id)).resolves.toBe(5000);
    expect(native.calls).toEqual(['download', 'acknowledge']);
  });

  it('path is <models root>/<model>/<file name>, for a Parakeet and a Qwen file', () => {
    const files = new ExpoModelFiles();
    expect(files.path('parakeet-tokens')).toBe(
      '/app/Library/Application Support/models/parakeet/tokens.txt',
    );
    expect(files.path(mockQwen.id)).toBe(
      '/app/Library/Application Support/models/qwen3-4b/Qwen3-4B-Q4_K_M.gguf',
    );
  });

  it('pause and resume use the same task; a restart rebuilds it from the saved state', async () => {
    const { task } = start();
    await settle();
    const native = lastTask();
    native.progress(1500);

    task.pause();
    await expect(task.done).resolves.toBe('paused');
    task.resume();
    await settle();
    expect(mockFs.tasks).toHaveLength(1);
    expect(native.calls).toEqual(['download', 'pause', 'resume']);

    // Paused again, then the app is restarted: a new adapter rebuilds the task from the kv store.
    task.pause();
    await expect(task.done).resolves.toBe('paused');
    const restarted = start(new ExpoModelFiles());
    await expect(restarted.files.bytesOnDisk(mockQwen.id)).resolves.toBe(1500);
    await settle();
    const rebuilt = lastTask();
    expect(rebuilt).not.toBe(native);
    expect(rebuilt.restoredFrom).toEqual({
      url: mockQwen.url,
      fileUri: QWEN_PART,
      isDirectory: false,
      resumeData: 'resume-data',
    });
    expect(rebuilt.calls).toEqual(['resume']);
    rebuilt.finish(5000);
    await expect(restarted.task.done).resolves.toBe('complete');
    expect(mockFs.disk.get(QWEN_FINAL)).toBe(5000);
  });

  it('a restart whose saved state cannot be rebuilt starts the file again from zero', async () => {
    const { task } = start();
    await settle();
    lastTask().progress(1500);
    task.pause();
    await task.done;

    mockFs.badSavable = true;
    start(new ExpoModelFiles());
    await settle();
    expect(lastTask().restoredFrom).toBeNull();
    expect(lastTask().calls).toEqual(['download']);
    expect(mockFs.disk.has(QWEN_PART)).toBe(false);
  });

  it('on Android a restart with no saved pause carries on from the part file', async () => {
    android = jest.replaceProperty(Platform, 'OS', 'android');
    start();
    await settle();
    expect(lastTask().dest).toBe(`file:///app/Documents/models/qwen3-4b/qwen3-4b-q4_k_m.part`);
    // The app is killed mid-download: no pause, so no saved state, but the part file has 1200 bytes.
    lastTask().progress(1200);

    start(new ExpoModelFiles());
    await settle();
    expect(lastTask().restoredFrom).toEqual({
      url: mockQwen.url,
      fileUri: 'file:///app/Documents/models/qwen3-4b/qwen3-4b-q4_k_m.part',
      isDirectory: false,
      resumeData: '1200',
    });
    expect(lastTask().calls).toEqual(['resume']);
  });

  it('on Android a restart carries on from the part file, not from an older saved pause', async () => {
    android = jest.replaceProperty(Platform, 'OS', 'android');
    const { task } = start();
    await settle();
    lastTask().progress(1500);
    task.pause();
    await task.done;
    // Resumed, and 2700 bytes on disk before the transfer stuck: the saved pause still says 1500.
    task.resume();
    await settle();
    lastTask().progress(2700);

    start(new ExpoModelFiles());
    await settle();
    expect(lastTask().restoredFrom).toEqual({
      url: mockQwen.url,
      fileUri: 'file:///app/Documents/models/qwen3-4b/qwen3-4b-q4_k_m.part',
      isDirectory: false,
      resumeData: '2700',
    });
  });

  it('a no-space failure resolves done as error space; a dropped connection as error network', async () => {
    const first = start();
    await settle();
    lastTask().fail('Unable to download file: ENOSPC (No space left on device)');
    await expect(first.task.done).resolves.toEqual({ error: 'space' });

    const second = start();
    await settle();
    lastTask().fail('The network connection was lost.');
    await expect(second.task.done).resolves.toEqual({ error: 'network' });

    const third = start();
    await settle();
    lastTask().fail('Unable to download file: 500');
    await expect(third.task.done).resolves.toEqual({ error: 'network' });
    expect(mockFs.disk.has(QWEN_FINAL)).toBe(false);
  });

  it('does not start on cellular when cellular is not allowed', async () => {
    mockNet.type = 'cellular';
    const blocked = start(new ExpoModelFiles(), false);
    await expect(blocked.task.done).resolves.toBe('paused');
    expect(mockFs.tasks).toHaveLength(0);

    // Back on Wi-Fi, resume starts it.
    mockNet.type = 'wifi';
    blocked.task.resume();
    await settle();
    expect(mockFs.tasks).toHaveLength(1);

    mockNet.type = 'cellular';
    start(new ExpoModelFiles(), true);
    await settle();
    expect(mockFs.tasks).toHaveLength(2);
  });

  it('every completed file is excluded from backup', async () => {
    const { task } = start();
    await settle();
    // The models root, once, when it is created.
    expect(jest.mocked(excludeFromBackup).mock.calls).toEqual([[ROOT]]);
    lastTask().finish(5000);
    await task.done;
    expect(jest.mocked(excludeFromBackup).mock.calls).toEqual([[ROOT], [QWEN_FINAL]]);
  });

  it('sha256 and remove go through the module and the file API', async () => {
    const files = new ExpoModelFiles();
    mockFs.disk.set(QWEN_FINAL, 5000);
    mockFs.disk.set(QWEN_PART, 10);
    await expect(files.sha256(mockQwen.id)).resolves.toBe(`digest-of-${QWEN_FINAL}`);
    expect(mockFs.digests).toEqual([`SHA-256 ${QWEN_FINAL}`]);

    await files.remove(mockQwen.id);
    expect(mockFs.disk.has(QWEN_FINAL)).toBe(false);
    expect(mockFs.disk.has(QWEN_PART)).toBe(false);
    await expect(files.bytesOnDisk(mockQwen.id)).resolves.toBe(0);
  });

  it('free space is the volume available to the app', async () => {
    await expect(new ExpoModelFiles().freeBytes()).resolves.toBe(9_000_000_000);
  });
});
