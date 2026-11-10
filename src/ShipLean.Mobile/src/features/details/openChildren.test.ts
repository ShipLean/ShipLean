import type { RollupCountDto } from '@shiplean/core/api/views';
import { describe, expect, it } from 'vitest';
import { closingWarning, openCount, openText } from './openChildren';

const rollup = (type: RollupCountDto['type'], open: number, closed = 0): RollupCountDto => ({
  type,
  open,
  closed,
  inCurrentSprint: 0,
  inOtherSprint: 0,
});

describe('openChildren', () => {
  it('words open stories, bugs and nested Features as the web does', () => {
    const rollups = [
      rollup('feature', 1),
      rollup('story', 2, 5),
      rollup('bug', 1),
      rollup('task', 9),
    ];
    // Tasks are not counted, and the nouns come in the web's order whatever the order of the roll-ups.
    expect(openText(rollups)).toBe('2 stories, 1 bug and 1 nested Feature');
    expect(openCount(rollups)).toBe(4);
    expect(openText([rollup('story', 3), rollup('bug', 2)])).toBe('3 stories and 2 bugs');
    expect(openText([rollup('story', 1)])).toBe('1 story');
    expect(openText([rollup('action', 2)])).toBe('2 action items');
    expect(closingWarning([rollup('story', 3), rollup('bug', 2)], 'completed')).toBe(
      '3 stories and 2 bugs still open',
    );
    expect(closingWarning([rollup('story', 1)], 'completed')).toBe('1 story still open');
  });

  it('says nothing when the target state is not completed', () => {
    const open = [rollup('story', 3)];
    for (const target of ['proposed', 'inProgress', 'resolved', 'removed'] as const) {
      expect(closingWarning(open, target)).toBeNull();
    }
    // And nothing when nothing is open, or only tasks are.
    expect(closingWarning([rollup('story', 0, 4), rollup('task', 3)], 'completed')).toBeNull();
    expect(openText([])).toBe('');
  });
});
