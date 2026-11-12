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

// What keeps the download going when the person leaves the app: Android's foreground service and its notification.
// start() also updates the percentage (-1: not known yet) and returns false when the platform refused to start it.
export interface DownloadKeepAlive {
  start(percent: number): boolean;
  stop(): void;
}

export interface NetworkKind {
  kind(): 'wifi' | 'cellular' | 'none';
  subscribe(cb: () => void): () => void;
}
