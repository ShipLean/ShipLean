import { describe, expect, it } from 'vitest';
import { reorderTarget } from './reorderTarget';

const ids = ['a', 'b', 'c', 'd'];

describe('reorderTarget', () => {
  it('reorderTarget_WhenDroppedOnAnEarlierRow_ShouldSitJustAboveIt', () => {
    expect(reorderTarget(ids, 'c', 'a')).toEqual({ ids: ['c', 'a', 'b', 'd'], beforeItemId: 'a' });
  });

  it('reorderTarget_WhenDroppedOnALaterRow_ShouldSitJustBelowIt', () => {
    expect(reorderTarget(ids, 'a', 'c')).toEqual({ ids: ['b', 'c', 'a', 'd'], beforeItemId: 'd' });
  });

  it('reorderTarget_WhenDroppedOnTheLastRow_ShouldGoLast', () => {
    expect(reorderTarget(ids, 'a', 'd')).toEqual({ ids: ['b', 'c', 'd', 'a'], beforeItemId: null });
  });

  it('reorderTarget_WhenDroppedOnItselfOrUnknown_ShouldBeNull', () => {
    expect(reorderTarget(ids, 'b', 'b')).toBeNull();
    expect(reorderTarget(ids, 'b', 'zzz')).toBeNull();
  });
});
