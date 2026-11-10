// The ports wave 1 builds the model download against (C9). ExpoModelFiles and NetInfoNetworkKind implement them; the
// model store (S1a) uses only these.
import type { MODEL_FILES } from '../manifest';

export type ModelFile = (typeof MODEL_FILES)[number];

export interface DownloadTask {
  pause(): void;
  resume(): void;
  done: Promise<'complete' | 'paused' | { error: 'network' | 'space' }>;
}

export interface ModelFiles {
  freeBytes(): Promise<number>;
  bytesOnDisk(id: string): Promise<number>;
  sha256(id: string): Promise<string>;
  remove(id: string): Promise<void>;
  path(id: string): string;
  download(
    file: ModelFile,
    opts: { onProgress(bytes: number): void; cellular: boolean },
  ): DownloadTask;
}

export interface NetworkKind {
  kind(): 'wifi' | 'cellular' | 'none';
  subscribe(cb: () => void): () => void;
}
