// Fakes of the model download ports (C9) for the model store's tests: no disk, no network.
import { MODEL_FILES, type ModelFile } from '../voice/manifest';
import type { DownloadTask, ModelFiles, NetworkKind } from '../voice/native/ports';

type Outcome = Awaited<DownloadTask['done']>;

const SIZES: Record<ModelFile['model'], number> = {
  parakeet: 600_000_000,
  'qwen3-1.7b': 1_100_000_000,
  'qwen3-4b': 2_500_000_000,
};

// MODEL_FILES with sizes and checksums, until the pinned ones land. A model split over several files shares its size
// between them.
export function sizedManifest(): ModelFile[] {
  return MODEL_FILES.map((f) => {
    const count = MODEL_FILES.filter((g) => g.model === f.model).length;
    return { ...f, bytes: Math.round(SIZES[f.model] / count), sha256: `sha256-${f.id}` };
  });
}

// One download the test drives: progress(), complete() and fail() play the network; pause() is the store's.
export class FakeDownloadTask implements DownloadTask {
  readonly done: Promise<Outcome>;
  paused = false;
  resumed = 0;
  settled: Outcome | null = null;
  private settle!: (outcome: Outcome) => void;

  constructor(
    readonly file: ModelFile,
    readonly cellular: boolean,
    // The bytes already on disk when it started: a resumed download carries on from them.
    readonly startBytes: number,
    private readonly files: FakeModelFiles,
    private readonly onProgress: (bytes: number) => void,
  ) {
    this.done = new Promise((resolve) => {
      this.settle = (outcome) => {
        if (this.settled !== null) return;
        this.settled = outcome;
        resolve(outcome);
      };
    });
  }

  progress(bytes: number): void {
    this.files.disk.set(this.file.id, bytes);
    this.onProgress(bytes);
  }

  complete(): void {
    this.progress(this.file.bytes);
    this.settle('complete');
  }

  fail(error: 'network' | 'space'): void {
    this.settle({ error });
  }

  pause(): void {
    this.paused = true;
    this.settle('paused');
  }

  resume(): void {
    this.resumed += 1;
  }
}

export class FakeModelFiles implements ModelFiles {
  readonly disk = new Map<string, number>();
  readonly downloads: FakeDownloadTask[] = [];
  readonly removed: string[] = [];
  readonly hashed: string[] = [];
  free: number;
  private readonly badSums = new Map<string, number>();

  constructor(
    private readonly manifest: readonly ModelFile[] = sizedManifest(),
    free = 32_000_000_000,
  ) {
    this.free = free;
  }

  // The next `times` checksums of this file come out wrong.
  wrongChecksum(id: string, times = 1): void {
    this.badSums.set(id, times);
  }

  // The newest download of a file, or the newest of all.
  task(id?: string): FakeDownloadTask {
    const task = [...this.downloads].reverse().find((t) => id === undefined || t.file.id === id);
    if (task === undefined) throw new Error(`no download${id === undefined ? '' : ` of ${id}`}`);
    return task;
  }

  // Puts a whole file on disk, as if downloaded before.
  place(id: string, bytes?: number): void {
    this.disk.set(id, bytes ?? this.file(id).bytes);
  }

  async freeBytes(): Promise<number> {
    return this.free;
  }

  async bytesOnDisk(id: string): Promise<number> {
    return this.disk.get(id) ?? 0;
  }

  async sha256(id: string): Promise<string> {
    this.hashed.push(id);
    const bad = this.badSums.get(id) ?? 0;
    if (bad > 0) {
      this.badSums.set(id, bad - 1);
      return 'sha256-wrong';
    }
    return this.file(id).sha256;
  }

  async remove(id: string): Promise<void> {
    this.removed.push(id);
    this.disk.delete(id);
  }

  path(id: string): string {
    return `/models/${id}`;
  }

  download(
    file: ModelFile,
    opts: { onProgress(bytes: number): void; cellular: boolean },
  ): DownloadTask {
    const task = new FakeDownloadTask(
      file,
      opts.cellular,
      this.disk.get(file.id) ?? 0,
      this,
      opts.onProgress,
    );
    this.downloads.push(task);
    return task;
  }

  private file(id: string): ModelFile {
    const file = this.manifest.find((f) => f.id === id);
    if (file === undefined) throw new Error(`no file ${id} in the manifest`);
    return file;
  }
}

export class FakeNetwork implements NetworkKind {
  private readonly listeners = new Set<() => void>();

  constructor(private current: 'wifi' | 'cellular' | 'none' = 'wifi') {}

  set(kind: 'wifi' | 'cellular' | 'none'): void {
    this.current = kind;
    for (const cb of [...this.listeners]) cb();
  }

  kind(): 'wifi' | 'cellular' | 'none' {
    return this.current;
  }

  subscribe(cb: () => void): () => void {
    this.listeners.add(cb);
    return () => {
      this.listeners.delete(cb);
    };
  }
}
