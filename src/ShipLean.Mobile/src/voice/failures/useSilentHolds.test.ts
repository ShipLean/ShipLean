import { beforeEach, describe, expect, it } from 'vitest';
import type { VoiceFailure } from '../contracts';
import { recordFailure, resetSilentHolds, SILENT_HOLDS_RESET_MS, silentHolds } from './silentHolds';

const silent = (): VoiceFailure => ({ kind: 'nothingHeard', detail: {} });
const short = (): VoiceFailure => ({ kind: 'tooShort', detail: { heldMs: 200 } });
const unclear = (): VoiceFailure => ({ kind: 'unclear', detail: { heard: 'hmm' } });
const noMatch = (): VoiceFailure => ({
  kind: 'noMatch',
  detail: { spoken: 'x', role: 'key', span: null },
});

describe('useSilentHolds', () => {
  beforeEach(resetSilentHolds);

  it('counts consecutive nothingHeard and resets on anything else', () => {
    expect(recordFailure(silent(), 0)).toBe(1);
    expect(recordFailure(silent(), 1000)).toBe(2);
    expect(recordFailure(silent(), 2000)).toBe(3);
    for (const other of [short, unclear, noMatch]) {
      expect(recordFailure(other(), 3000)).toBe(0);
      expect(recordFailure(silent(), 4000)).toBe(1);
      expect(recordFailure(silent(), 5000)).toBe(2);
    }
  });

  it('counts the same failure once', () => {
    const f = silent();
    expect(recordFailure(f, 0)).toBe(1);
    expect(recordFailure(f, 10)).toBe(1);
    expect(silentHolds()).toBe(1);
  });

  it('starts again after two minutes with no silent hold', () => {
    recordFailure(silent(), 0);
    recordFailure(silent(), 1000);
    expect(recordFailure(silent(), 1000 + SILENT_HOLDS_RESET_MS)).toBe(3);
    expect(recordFailure(silent(), 1000 + 2 * SILENT_HOLDS_RESET_MS + 1)).toBe(1);
  });
});
