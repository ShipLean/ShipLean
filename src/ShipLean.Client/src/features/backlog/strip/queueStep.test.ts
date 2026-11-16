import { describe, expect, it } from 'vitest';
import { queueStep } from './queueStep';

const ids = ['a', 'b', 'c'];
const all = new Set(ids);
const none = new Set<string>();

describe('queueStep', () => {
  it('queueStep_WhenNothingPassed_ShouldBeOnTheFirstOfAll', () => {
    expect(queueStep(ids, all, none)).toEqual({ id: 'a', position: 1, total: 3 });
  });

  it('queueStep_WhenTheFirstIsPassed_ShouldStepToTheNextAndKeepTheTotal', () => {
    expect(queueStep(ids, all, new Set(['a']))).toEqual({ id: 'b', position: 2, total: 3 });
  });

  it('queueStep_WhenAnItemIsNoLongerFlagged_ShouldStepOverIt', () => {
    expect(queueStep(ids, new Set(['a', 'c']), new Set(['a']))).toEqual({
      id: 'c',
      position: 3,
      total: 3,
    });
  });

  it('queueStep_WhenEveryItemIsPassedOrEstimated_ShouldBeNull', () => {
    expect(queueStep(ids, new Set(['a', 'b']), new Set(['a', 'b']))).toBeNull();
  });
});
