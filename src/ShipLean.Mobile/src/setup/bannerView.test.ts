import { describe, expect, it } from 'vitest';
import type { ModelStatus } from '../voice/contracts';
import type { ModelFile } from '../voice/manifest';
import { bannerView, memoryView } from './bannerView';

const status = (over: Partial<ModelStatus>): ModelStatus => ({
  phase: 'ready',
  choice: 'auto',
  resolved: 'qwen3-4b',
  bytesDone: 0,
  bytesTotal: 0,
  freeBytes: 0,
  wifiOnly: true,
  reason: null,
  files: [],
  ...over,
});
const offered = { setupOffered: true, left: null };

describe('bannerView', () => {
  it('downloading, paused for Wi-Fi, skipped, memory and ready give the right banner or none', () => {
    const downloading = bannerView(
      status({ phase: 'downloading', bytesDone: 2_000_000_000, bytesTotal: 3_100_000_000 }),
      { setupOffered: true, left: 'about 3 min' },
    );
    expect(downloading).toEqual({
      kind: 'gettingReady',
      text: 'Voice is getting ready · 2.0 of 3.1 GB on Wi-Fi · about 3 min. Read, tap and type in the meantime.',
      fraction: 2 / 3.1,
    });

    const paused = bannerView(
      status({ phase: 'paused', reason: 'wifi', bytesDone: 1_000, bytesTotal: 4_000 }),
      offered,
    );
    expect(paused).toEqual({
      kind: 'paused',
      text: 'Voice download paused · waiting for Wi-Fi',
      fraction: 0.25,
    });

    expect(bannerView(status({ phase: 'skipped' }), { setupOffered: false, left: null })).toEqual({
      kind: 'off',
      text: 'Voice is off',
      action: 'Set up',
    });
    expect(bannerView(status({ phase: 'none' }), offered)?.kind).toBe('off');
    expect(bannerView(status({ phase: 'none' }), { setupOffered: false, left: null })).toBeNull();

    expect(bannerView(status({ phase: 'failed', reason: 'memory' }), offered)?.kind).toBe('memory');
    expect(bannerView(status({ phase: 'failed', reason: 'checksum' }), offered)).toBeNull();

    expect(bannerView(status({ phase: 'ready' }), offered)).toBeNull();
    // A model change keeps the phase ready, however much it has downloaded.
    expect(
      bannerView(status({ phase: 'ready', reason: 'wifi', bytesDone: 5, bytesTotal: 10 }), offered),
    ).toBeNull();
  });

  it('leaves out the Wi-Fi words when Wi-Fi only is off, and the time before there is a rate', () => {
    const view = bannerView(
      status({
        phase: 'checking',
        wifiOnly: false,
        bytesDone: 3_100_000_000,
        bytesTotal: 3_100_000_000,
      }),
      offered,
    );
    expect(view).toMatchObject({
      text: 'Voice is getting ready · 3.1 of 3.1 GB. Read, tap and type in the meantime.',
    });
  });
});

describe('memoryView', () => {
  const file = (id: string, model: ModelFile['model'], name: string, bytes: number) =>
    ({ id, model, name, bytes }) as ModelFile;
  const files = [
    file('p', 'parakeet', 'Parakeet', 600),
    file('a', 'qwen3-1.7b', 'Qwen3 1.7B', 1_100_000_000),
    file('b', 'qwen3-4b', 'Qwen3 4B', 2_500_000_000),
  ];

  it('offers the 1.7B model after the 4B one failed, and nothing after 1.7B failed', () => {
    expect(memoryView(status({ resolved: 'qwen3-4b' }), files)).toEqual({
      failed: 'Qwen3 4B',
      smaller: { model: 'qwen3-1.7b', name: 'Qwen3 1.7B', bytes: 1_100_000_000 },
    });
    expect(memoryView(status({ resolved: 'qwen3-1.7b' }), files).smaller).toBeNull();
  });
});
