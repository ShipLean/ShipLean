import { describe, expect, it } from 'vitest';
import { changesText, countChanges } from './countChanges';

const row = (id: string, revision: number) => ({ id, revision });

describe('countChanges', () => {
  it('countChanges_WhenNothingDiffers_ShouldBeZero', () => {
    expect(countChanges([row('a', 1), row('b', 2)], [row('a', 1), row('b', 2)])).toBe(0);
  });

  it('countChanges_WhenRevisionAddedAndRemoved_ShouldCountEach', () => {
    const before = [row('a', 1), row('b', 2), row('c', 1)];
    const after = [row('a', 2), row('b', 2), row('d', 1)];
    expect(countChanges(before, after)).toBe(3);
  });

  it('countChanges_WhenOneItemReordered_ShouldCountOne', () => {
    const before = [row('a', 1), row('b', 1), row('c', 1), row('d', 1)];
    expect(countChanges(before, [row('c', 1), row('a', 1), row('b', 1), row('d', 1)])).toBe(1);
    expect(countChanges(before, [row('b', 1), row('c', 1), row('d', 1), row('a', 1)])).toBe(1);
  });

  it('countChanges_WhenAnItemIsAddedAbove_ShouldCountOnlyIt', () => {
    expect(countChanges([row('a', 1), row('b', 1)], [row('n', 1), row('a', 1), row('b', 1)])).toBe(
      1,
    );
  });

  it('countChanges_WhenAnItemIsEditedAndMoved_ShouldCountItOnce', () => {
    expect(
      countChanges(
        [row('a', 1), row('b', 1), row('c', 1)],
        [row('c', 2), row('a', 1), row('b', 1)],
      ),
    ).toBe(1);
  });

  it('changesText_ShouldPluralise', () => {
    expect(changesText(0)).toContain('Nothing changed');
    expect(changesText(1)).toContain('1 item changed');
    expect(changesText(4)).toContain('4 items changed');
  });
});
