import { describe, expect, it } from 'vitest';
import type { ModelStatus } from '../voice/contracts';
import { MODEL_FILES, type ModelFile } from '../voice/manifest';
import { downloadView, noSpaceView, rateOf, timeLeft, withSample } from './downloadView';

const SIZES = { parakeet: 600_000_000, 'qwen3-1.7b': 1_100_000_000, 'qwen3-4b': 2_500_000_000 };
// Parakeet split over two files, as the pinned manifest may have it.
const FILES: ModelFile[] = [
  ...MODEL_FILES.filter((f) => f.model === 'parakeet').flatMap((f) => [
    { ...f, id: 'p-a', bytes: 200_000_000 },
    { ...f, id: 'p-b', bytes: 400_000_000 },
  ]),
  ...MODEL_FILES.filter((f) => f.model !== 'parakeet').map((f) => ({
    ...f,
    bytes: SIZES[f.model],
  })),
];

const status = (over: Partial<ModelStatus>): ModelStatus => ({
  phase: 'downloading',
  choice: 'auto',
  resolved: 'qwen3-4b',
  bytesDone: 0,
  bytesTotal: 3_100_000_000,
  freeBytes: 80_000_000_000,
  wifiOnly: true,
  reason: null,
  files: [],
  ...over,
});

describe('downloadView', () => {
  it('shows overall progress and a row per model, summing a model split over files', () => {
    const view = downloadView(
      status({
        bytesDone: 1_200_000_000,
        files: [
          { id: 'p-a', state: 'checked', bytes: 200_000_000 },
          { id: 'p-b', state: 'checked', bytes: 400_000_000 },
          { id: 'qwen3-4b-q4_k_m', state: 'downloading', bytes: 600_000_000 },
        ],
      }),
      FILES,
    );
    expect(view.percent).toBe(38);
    expect(view.amount).toBe('1.2 of 3.1 GB');
    expect(view.rows.map((r) => [r.name, r.state, r.detail])).toEqual([
      ['Parakeet TDT 0.6B', 'checked', 'Speech to text · downloaded and checked'],
      ['Qwen3 4B', 'downloading', 'Command model · 0.6 of 2.5 GB'],
    ]);
    expect(view.rows[1].fraction).toBeCloseTo(0.24);
  });

  it('marks files not started as waiting and failed ones as failed', () => {
    const view = downloadView(
      status({
        files: [
          { id: 'p-a', state: 'failed', bytes: 0 },
          { id: 'p-b', state: 'pending', bytes: 0 },
          { id: 'qwen3-4b-q4_k_m', state: 'pending', bytes: 0 },
        ],
      }),
      FILES,
    );
    expect(view.rows.map((r) => r.state)).toEqual(['failed', 'waiting']);
    expect(view.percent).toBe(0);
  });

  it('is at 100 when ready and has no rows before anything is fetched', () => {
    expect(downloadView(status({ phase: 'ready', bytesDone: 5 }), FILES).percent).toBe(100);
    expect(downloadView(status({ phase: 'none', bytesTotal: 0 }), FILES).rows).toEqual([]);
  });

  it('works out the time left from the rate over the last half minute', () => {
    let samples = withSample([], { at: 0, bytes: 0 });
    expect(rateOf(samples)).toBeNull();
    samples = withSample(samples, { at: 1_000, bytes: 10_000_000 });
    expect(rateOf(samples)).toBeNull();
    samples = withSample(samples, { at: 10_000, bytes: 100_000_000 });
    expect(rateOf(samples)).toBe(10_000_000);
    expect(timeLeft(3_600_000_000, 10_000_000)).toBe('about 6 min');
    expect(timeLeft(100_000_000, 10_000_000)).toBe('less than a minute');
    expect(timeLeft(40_000_000_000, 10_000_000)).toBe('about 1 h 7 min');
    expect(timeLeft(1, null)).toBeNull();
    // Old samples fall out of the window.
    const later = withSample(samples, { at: 60_000, bytes: 100_000_000 });
    expect(later).toHaveLength(1);
  });
});

describe('noSpaceView', () => {
  const stopped = (free: number, files: ModelStatus['files']) =>
    status({
      phase: 'failed',
      reason: 'space',
      bytesDone: 2_300_000_000,
      freeBytes: free,
      files: [{ id: 'p-a', state: 'checked', bytes: 200_000_000 }, ...files],
    });

  it('gives what is left, what is free, and the smaller model when it fits once the partial 4B is removed', () => {
    const view = noSpaceView(
      stopped(300_000_000, [{ id: 'qwen3-4b-q4_k_m', state: 'failed', bytes: 1_700_000_000 }]),
      FILES,
    );
    expect(view?.neededBytes).toBe(800_000_000);
    expect(view?.freeBytes).toBe(300_000_000);
    expect(view?.model).toBe('Qwen3 4B');
    expect(view?.smaller).toEqual({
      model: 'qwen3-1.7b',
      name: 'Qwen3 1.7B',
      bytes: 1_100_000_000,
    });
  });

  it('offers no smaller model when it would not fit either', () => {
    const view = noSpaceView(
      stopped(100_000_000, [{ id: 'qwen3-4b-q4_k_m', state: 'failed', bytes: 500_000_000 }]),
      FILES,
    );
    expect(view?.smaller).toBeNull();
  });

  it('throws when the stopped model has no manifest entry', () => {
    expect(() => noSpaceView(stopped(300_000_000, []), [])).toThrow(/manifest has no/);
  });

  it('is null when the download did not stop for space', () => {
    expect(noSpaceView(status({ reason: 'network' }), FILES)).toBeNull();
  });
});
