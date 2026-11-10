import { describe, expect, it } from 'vitest';
import { workItemExecutor } from './executor';

describe('workItemExecutor', () => {
  it('marks the field kinds and addNote reversible and the asks kinds and confirmStatus not', () => {
    for (const kind of ['setState', 'assign', 'addNote', 'setHealth', 'setStatus'] as const) {
      expect(workItemExecutor.reversible(kind)).toBe(true);
    }
    for (const kind of ['approve', 'discuss', 'reply', 'confirmStatus'] as const) {
      expect(workItemExecutor.reversible(kind)).toBe(false);
    }
  });
});
