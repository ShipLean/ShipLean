import { memoryStore } from '@shiplean/core/platform';
import type { KeyValueStore } from '@shiplean/core/platform';
import { describe, expect, it } from 'vitest';
import { FakeModelFiles, FakeNetwork, sizedManifest } from '../test-support/modelFiles';
import type { ModelStatus } from '../voice/contracts';
import { chooseModel, type ModelId } from '../voice/manifest';
import { createModelStore } from './modelStore';

const GIB = 1024 ** 3;
const manifest = sizedManifest();

function ids(model: ModelId): string[] {
  return manifest.filter((f) => f.model === model).map((f) => f.id);
}

function bytes(...models: ModelId[]): number {
  return manifest.filter((f) => models.includes(f.model)).reduce((sum, f) => sum + f.bytes, 0);
}

// Lets the store's awaits run.
async function flush(): Promise<void> {
  await new Promise<void>((resolve) => setTimeout(resolve, 0));
}

function makeStore(
  opts: {
    gb?: number;
    kv?: KeyValueStore;
    files?: FakeModelFiles;
    network?: FakeNetwork;
  } = {},
) {
  const kv = opts.kv ?? memoryStore();
  const files = opts.files ?? new FakeModelFiles(manifest);
  const network = opts.network ?? new FakeNetwork('wifi');
  const store = createModelStore({
    files,
    network,
    kv,
    memoryBytes: (opts.gb ?? 11.2) * GIB,
    manifest,
    clock: () => new Date(0),
  });
  return { store, files, network, kv };
}

// Completes every file of these models in order, as the network would.
async function complete(files: FakeModelFiles, ...models: ModelId[]): Promise<void> {
  for (const model of models) {
    for (const id of ids(model)) {
      await flush();
      files.task(id).complete();
      await flush();
    }
  }
}

async function readyOn(gb: number, ...models: ModelId[]) {
  const made = makeStore({ gb });
  made.store.download();
  await complete(made.files, 'parakeet', ...models);
  expect(made.store.status().phase).toBe('ready');
  return made;
}

describe('modelStore', () => {
  it('auto picks 4B at 11.2 GB reported, 1.7B at 7.4 GB, and nothing under 6 GB', async () => {
    const big = makeStore({ gb: 11.2 }).store;
    expect(big.deviceMemory()).toEqual({ reportedGb: 11.2, soldGb: 12 });
    expect(big.status()).toMatchObject({ phase: 'none', choice: 'auto', resolved: 'qwen3-4b' });

    const eight = makeStore({ gb: 7.4 }).store;
    expect(eight.deviceMemory()).toEqual({ reportedGb: 7.4, soldGb: 8 });
    expect(eight.status().resolved).toBe('qwen3-1.7b');

    const small = makeStore({ gb: 3.7 });
    expect(small.store.deviceMemory().soldGb).toBe(4);
    expect(small.store.status().resolved).toBeNull();
    small.store.choose('qwen3-1.7b');
    expect(small.store.status().resolved).toBeNull();
    small.store.download();
    await flush();
    expect(small.store.status().phase).toBe('none');
    expect(small.files.downloads).toHaveLength(0);
  });

  it('4B is refused on a 6 GB phone and warned on 8 GB', () => {
    const six = makeStore({ gb: 5.6 }).store;
    expect(six.deviceMemory().soldGb).toBe(6);
    six.choose('qwen3-4b');
    expect(six.status()).toMatchObject({ choice: 'auto', resolved: 'qwen3-1.7b' });

    const eight = makeStore({ gb: 7.4 }).store;
    eight.choose('qwen3-4b');
    expect(eight.status()).toMatchObject({ choice: 'qwen3-4b', resolved: 'qwen3-4b' });
    // The warning is the screen's, from the same rule and the memory the store reports.
    expect(chooseModel(eight.deviceMemory().reportedGb).fourB).toBe('warn');
  });

  it('downloads Parakeet then the command model with progress over both', async () => {
    const { store, files } = makeStore({ gb: 11.2 });
    const phases: ModelStatus['phase'][] = [];
    store.subscribe(() => phases.push(store.status().phase));
    store.download();
    await flush();

    const [parakeet] = ids('parakeet');
    const [fourB] = ids('qwen3-4b');
    expect(files.downloads.map((t) => t.file.id)).toEqual([parakeet]);
    expect(store.status()).toMatchObject({
      phase: 'downloading',
      bytesDone: 0,
      bytesTotal: bytes('parakeet', 'qwen3-4b'),
    });

    files.task(parakeet).progress(200_000_000);
    expect(store.status().bytesDone).toBe(200_000_000);
    expect(store.status().files.find((f) => f.id === parakeet)).toEqual({
      id: parakeet,
      state: 'downloading',
      bytes: 200_000_000,
    });

    await complete(files, 'parakeet');
    expect(files.hashed).toEqual(ids('parakeet'));
    expect(files.downloads.map((t) => t.file.id)).toEqual([...ids('parakeet'), fourB]);
    files.task(fourB).progress(1_000_000_000);
    expect(store.status()).toMatchObject({
      phase: 'downloading',
      bytesDone: bytes('parakeet') + 1_000_000_000,
      bytesTotal: bytes('parakeet', 'qwen3-4b'),
    });

    await complete(files, 'qwen3-4b');
    expect(phases).toContain('checking');
    expect(store.status()).toMatchObject({
      phase: 'ready',
      resolved: 'qwen3-4b',
      reason: null,
      bytesDone: bytes('parakeet', 'qwen3-4b'),
    });
    expect(store.status().files.map((f) => f.state)).toEqual(['checked', 'checked']);
  });

  it('pauses for Wi-Fi on mobile data, and useMobileDataOnce continues this download only', async () => {
    const network = new FakeNetwork('wifi');
    const { store, files } = makeStore({ gb: 11.2, network });
    const [parakeet] = ids('parakeet');
    store.download();
    await flush();
    files.task(parakeet).progress(100_000_000);

    network.set('cellular');
    await flush();
    expect(store.status()).toMatchObject({ phase: 'paused', reason: 'wifi', wifiOnly: true });
    expect(files.task(parakeet).paused).toBe(true);

    store.useMobileDataOnce();
    await flush();
    expect(store.status()).toMatchObject({ phase: 'downloading', reason: null, wifiOnly: true });
    expect(files.task(parakeet)).toMatchObject({ cellular: true, startBytes: 100_000_000 });
    await complete(files, 'parakeet', 'qwen3-4b');
    expect(store.status().phase).toBe('ready');

    // A later download, the model change, waits for Wi-Fi again.
    const before = files.downloads.length;
    store.choose('qwen3-1.7b');
    await flush();
    expect(files.downloads).toHaveLength(before);
    expect(store.status()).toMatchObject({ phase: 'ready', resolved: 'qwen3-4b', reason: 'wifi' });

    network.set('wifi');
    await flush();
    expect(files.task(ids('qwen3-1.7b')[0])).toMatchObject({ cellular: false });
    expect(store.status().reason).toBeNull();
  });

  it('a dropped connection pauses and resumes from the bytes on disk', async () => {
    const network = new FakeNetwork('wifi');
    const { store, files } = makeStore({ gb: 11.2, network });
    const [parakeet] = ids('parakeet');
    store.download();
    await flush();
    files.task(parakeet).progress(250_000_000);
    network.set('none');
    files.task(parakeet).fail('network');
    await flush();
    expect(store.status()).toMatchObject({
      phase: 'paused',
      reason: 'network',
      bytesDone: 250_000_000,
    });
    expect(files.removed).toEqual([]);

    network.set('wifi');
    await flush();
    expect(files.downloads).toHaveLength(2);
    expect(files.task(parakeet).startBytes).toBe(250_000_000);
    expect(store.status()).toMatchObject({ phase: 'downloading', bytesDone: 250_000_000 });
    await complete(files, 'parakeet', 'qwen3-4b');
    expect(store.status().phase).toBe('ready');
  });

  it('not enough space pauses with reason space and keeps the downloaded files', async () => {
    // Room for Parakeet, not for 4B.
    const files = new FakeModelFiles(manifest, bytes('parakeet') + 1_000_000_000);
    const { store } = makeStore({ gb: 11.2, files });
    const [fourB] = ids('qwen3-4b');
    store.download();
    await complete(files, 'parakeet');
    expect(store.status()).toMatchObject({ phase: 'paused', reason: 'space' });
    expect(files.downloads.map((t) => t.file.model)).toEqual(ids('parakeet').map(() => 'parakeet'));

    // A space error mid-file keeps the partial file too.
    files.free = 10_000_000_000;
    store.download();
    await flush();
    files.task(fourB).progress(700_000_000);
    files.task(fourB).fail('space');
    await flush();
    expect(store.status()).toMatchObject({ phase: 'paused', reason: 'space' });
    expect(files.removed).toEqual([]);
    expect(files.disk.get(fourB)).toBe(700_000_000);
    expect(store.status().files.find((f) => f.id === ids('parakeet')[0])?.state).toBe('checked');

    // Choosing 1.7B removes the partial 4B first, then downloads 1.7B.
    store.choose('qwen3-1.7b');
    await flush();
    await flush();
    expect(files.removed).toEqual([fourB]);
    expect(files.task().file.id).toBe(ids('qwen3-1.7b')[0]);
    expect(store.status()).toMatchObject({ phase: 'downloading', resolved: 'qwen3-1.7b' });
  });

  it('a checksum mismatch downloads the file once more; a second mismatch fails with reason checksum', async () => {
    const first = makeStore({ gb: 11.2 });
    const [parakeet] = ids('parakeet');
    first.files.wrongChecksum(parakeet, 1);
    first.store.download();
    await complete(first.files, 'parakeet');
    expect(first.files.removed).toEqual([parakeet]);
    expect(first.files.downloads.map((t) => t.file.id)).toEqual([parakeet, parakeet]);
    await complete(first.files, 'parakeet', 'qwen3-4b');
    expect(first.store.status()).toMatchObject({ phase: 'ready', reason: null });

    const second = makeStore({ gb: 11.2 });
    const [fourB] = ids('qwen3-4b');
    second.files.wrongChecksum(fourB, 2);
    second.store.download();
    await complete(second.files, 'parakeet', 'qwen3-4b', 'qwen3-4b');
    expect(second.store.status()).toMatchObject({ phase: 'failed', reason: 'checksum' });
    expect(second.store.status().files.find((f) => f.id === fourB)?.state).toBe('failed');
    expect(second.files.downloads.filter((t) => t.file.id === fourB)).toHaveLength(2);
    expect(second.files.removed).toEqual([fourB, fourB]);
  });

  it('changing model keeps phase ready on the old model until the new one is checked, then removes the old file', async () => {
    const { store, files } = await readyOn(11.2, 'qwen3-4b');
    const resolvedAtRemove: (string | null)[] = [];
    const remove = files.remove.bind(files);
    files.remove = async (id) => {
      resolvedAtRemove.push(store.status().resolved);
      await remove(id);
    };
    const [oneSeven] = ids('qwen3-1.7b');

    store.choose('qwen3-1.7b');
    await flush();
    expect(files.task().file.id).toBe(oneSeven);
    expect(store.status()).toMatchObject({
      phase: 'ready',
      choice: 'qwen3-1.7b',
      resolved: 'qwen3-4b',
      bytesDone: 0,
      bytesTotal: bytes('qwen3-1.7b'),
    });
    files.task(oneSeven).progress(500_000_000);
    expect(store.status()).toMatchObject({
      phase: 'ready',
      resolved: 'qwen3-4b',
      bytesDone: 500_000_000,
    });
    expect(files.removed).toEqual([]);

    await complete(files, 'qwen3-1.7b');
    expect(store.status()).toMatchObject({ phase: 'ready', resolved: 'qwen3-1.7b' });
    expect(files.removed).toEqual(ids('qwen3-4b'));
    expect(resolvedAtRemove).toEqual(ids('qwen3-4b').map(() => 'qwen3-1.7b'));
    expect(files.disk.has(ids('qwen3-4b')[0])).toBe(false);
  });

  it('deleteAll removes the files and keeps the choice and Wi-Fi only', async () => {
    const { store, files, kv } = await readyOn(7.4, 'qwen3-1.7b');
    store.choose('qwen3-1.7b');
    store.setWifiOnly(false);

    await store.deleteAll();
    expect(files.removed.sort()).toEqual([...ids('parakeet'), ...ids('qwen3-1.7b')].sort());
    expect(files.disk.size).toBe(0);
    expect(store.status()).toMatchObject({
      phase: 'none',
      choice: 'qwen3-1.7b',
      wifiOnly: false,
      resolved: 'qwen3-1.7b',
      bytesDone: 0,
    });

    const reloaded = makeStore({ gb: 7.4, kv, files }).store;
    expect(reloaded.status()).toMatchObject({
      phase: 'none',
      choice: 'qwen3-1.7b',
      wifiOnly: false,
    });
  });

  it('a store created over saved state continues the download', async () => {
    const { store, files, kv } = makeStore({ gb: 11.2 });
    const [fourB] = ids('qwen3-4b');
    store.download();
    await complete(files, 'parakeet');
    files.task(fourB).progress(1_200_000_000);

    // The app restarts: a new store over the same phone.
    const restarted = makeStore({ gb: 11.2, kv, files }).store;
    await flush();
    expect(files.task(fourB).startBytes).toBe(1_200_000_000);
    expect(files.downloads.filter((t) => t.file.model === 'parakeet')).toHaveLength(
      ids('parakeet').length,
    );
    expect(restarted.status()).toMatchObject({
      phase: 'downloading',
      bytesDone: bytes('parakeet') + 1_200_000_000,
    });
    await complete(files, 'qwen3-4b');
    expect(restarted.status()).toMatchObject({ phase: 'ready', resolved: 'qwen3-4b' });
  });

  it('reportLoadFailure memory fails with reason memory and starts no download', async () => {
    const { store, files } = await readyOn(7.4, 'qwen3-1.7b');
    store.choose('qwen3-4b');
    await complete(files, 'qwen3-4b');
    expect(store.status()).toMatchObject({ phase: 'ready', resolved: 'qwen3-4b' });
    const before = files.downloads.length;

    store.reportLoadFailure('memory');
    store.download();
    await flush();
    expect(store.status()).toMatchObject({
      phase: 'failed',
      reason: 'memory',
      resolved: 'qwen3-4b',
    });
    expect(files.downloads).toHaveLength(before);
    expect(files.removed).toEqual(ids('qwen3-1.7b'));

    // The sheet's "Switch to Qwen3 1.7B" is the way out: voice stays off until 1.7B is checked.
    store.choose('qwen3-1.7b');
    await flush();
    await flush();
    expect(store.status()).toMatchObject({ phase: 'downloading', resolved: 'qwen3-1.7b' });
    expect(files.task().file.id).toBe(ids('qwen3-1.7b')[0]);
  });
});
