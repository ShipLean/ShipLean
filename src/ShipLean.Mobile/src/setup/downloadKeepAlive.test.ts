import { memoryStore } from '@shiplean/core/platform';
import { describe, expect, it } from 'vitest';
import { FakeModelFiles, FakeNetwork, sizedManifest } from '../test-support/modelFiles';
import type { DownloadKeepAlive } from '../voice/native/ports';
import { keepDownloadAlive } from './downloadKeepAlive';
import { createModelStore } from './modelStore';

const GIB = 1024 ** 3;
const manifest = sizedManifest();

class FakeKeepAlive implements DownloadKeepAlive {
  readonly calls: (number | 'stop')[] = [];
  refuse = false;

  start(percent: number): boolean {
    this.calls.push(percent);
    return !this.refuse;
  }

  stop(): void {
    this.calls.push('stop');
  }
}

async function flush(): Promise<void> {
  await new Promise<void>((resolve) => setTimeout(resolve, 0));
}

function setUp(network = new FakeNetwork('wifi')) {
  const files = new FakeModelFiles(manifest);
  const store = createModelStore({
    files,
    network,
    kv: memoryStore(),
    memoryBytes: 11.2 * GIB,
    manifest,
    clock: () => new Date(0),
  });
  const keepAlive = new FakeKeepAlive();
  keepDownloadAlive(store, keepAlive);
  return { store, files, keepAlive, network };
}

// The 4B download: Parakeet then Qwen3 4B, 3.1 GB in all with the fake sizes.
const total = manifest
  .filter((f) => f.model === 'parakeet' || f.model === 'qwen3-4b')
  .reduce((sum, f) => sum + f.bytes, 0);

describe('keepDownloadAlive', () => {
  it('starts with the download, sends whole-percent changes only, and stops when the person pauses', async () => {
    const { store, files, keepAlive } = setUp();
    expect(keepAlive.calls).toEqual([]);
    store.download();
    await flush();
    const task = files.task();
    task.progress(Math.floor(total * 0.0101));
    task.progress(Math.floor(total * 0.0102));
    task.progress(Math.floor(total * 0.025));
    expect(keepAlive.calls).toEqual([0, 1, 2]);

    store.pause();
    expect(keepAlive.calls.at(-1)).toBe('stop');
  });

  it('stays up while a dropped connection waits for the retry, and stops while offline is waiting for Wi-Fi', async () => {
    const network = new FakeNetwork('wifi');
    const { store, files, keepAlive } = setUp(network);
    store.download();
    await flush();
    files.task().fail('network');
    await flush();
    expect(store.status()).toMatchObject({ phase: 'paused', reason: 'network' });
    expect(keepAlive.calls).not.toContain('stop');

    network.set('cellular');
    await flush();
    expect(store.status()).toMatchObject({ phase: 'paused', reason: 'wifi' });
    expect(keepAlive.calls.at(-1)).toBe('stop');
  });

  it('a refused start is tried again at the next percentage', async () => {
    const { store, files, keepAlive } = setUp();
    keepAlive.refuse = true;
    store.download();
    await flush();
    files.task().progress(Math.floor(total * 0.015));
    keepAlive.refuse = false;
    files.task().progress(Math.floor(total * 0.025));
    expect(keepAlive.calls).toEqual([0, 1, 2]);

    store.pause();
    expect(keepAlive.calls.at(-1)).toBe('stop');
  });
});
