import { describe, expect, it } from 'vitest';
import { featuresExecutor } from './executor';

describe('featuresExecutor', () => {
  it('marks the field kinds, moveRank and addNote reversible and approve, answer and markDigestSeen not', () => {
    for (const kind of ['setState', 'setHealth', 'setStatus', 'moveRank', 'addNote'] as const) {
      expect(featuresExecutor.reversible(kind)).toBe(true);
    }
    for (const kind of ['approve', 'answer', 'markDigestSeen'] as const) {
      expect(featuresExecutor.reversible(kind)).toBe(false);
    }
  });
});
