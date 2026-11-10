import { describe, expect, it } from 'vitest';
import { rankPlan } from './rankPlan';

const rows = ['A', 'B', 'C', 'D', 'E'].map((key, i) => ({ id: `id-${key}`, key, rank: i + 1 }));
const first = { slot: 'first', beforeItemId: null };
const last = { slot: 'last', beforeItemId: null };
const before = (key: string) => ({ slot: 'before', beforeItemId: `id-${key}` });

describe('rankPlan', () => {
  it('moves to the top as first and shifts the rows above down one', () => {
    const plan = rankPlan(rows, 'id-D', { to: 'top' });
    expect(plan).toMatchObject({ placement: first, fromRank: 4, toRank: 1 });
    expect(plan?.shifted).toEqual([
      { key: 'A', from: 1, to: 2 },
      { key: 'B', from: 2, to: 3 },
      { key: 'C', from: 3, to: 4 },
    ]);
  });

  it('moves to the bottom as last', () => {
    const plan = rankPlan(rows, 'id-B', { to: 'bottom' });
    expect(plan).toMatchObject({ placement: last, fromRank: 2, toRank: 5 });
    expect(plan?.shifted).toEqual([
      { key: 'C', from: 3, to: 2 },
      { key: 'D', from: 4, to: 3 },
      { key: 'E', from: 5, to: 4 },
    ]);
  });

  it('places below X before the row after X, or last', () => {
    expect(rankPlan(rows, 'id-A', { to: 'below', otherId: 'id-C' })?.placement).toEqual(
      before('D'),
    );
    expect(rankPlan(rows, 'id-A', { to: 'below', otherId: 'id-E' })?.placement).toEqual(last);
    expect(rankPlan(rows, 'id-E', { to: 'above', otherId: 'id-B' })?.placement).toEqual(
      before('B'),
    );
    const down = rankPlan(rows, 'id-A', { to: 'below', otherId: 'id-C' });
    expect(down).toMatchObject({ fromRank: 1, toRank: 3 });
  });

  it('moves up or down by n and to rank n, clamped', () => {
    expect(rankPlan(rows, 'id-D', { to: 'up', by: 2 })).toMatchObject({
      toRank: 2,
      placement: before('B'),
    });
    expect(rankPlan(rows, 'id-B', { to: 'down', by: 2 })).toMatchObject({
      toRank: 4,
      placement: before('E'),
    });
    expect(rankPlan(rows, 'id-B', { to: 'up', by: 9 })).toMatchObject({
      toRank: 1,
      placement: first,
    });
    expect(rankPlan(rows, 'id-B', { to: 'down', by: 9 })).toMatchObject({
      toRank: 5,
      placement: last,
    });
    expect(rankPlan(rows, 'id-E', { to: 'rank', rank: 2 })).toMatchObject({
      toRank: 2,
      placement: before('B'),
    });
    expect(rankPlan(rows, 'id-C', { to: 'rank', rank: 99 })).toMatchObject({
      toRank: 5,
      placement: last,
    });
    expect(rankPlan(rows, 'id-C', { to: 'rank', rank: -3 })).toMatchObject({
      toRank: 1,
      placement: first,
    });
  });

  it('returns null when nothing moves', () => {
    expect(rankPlan(rows, 'id-A', { to: 'top' })).toBeNull();
    expect(rankPlan(rows, 'id-E', { to: 'bottom' })).toBeNull();
    expect(rankPlan(rows, 'id-C', { to: 'rank', rank: 3 })).toBeNull();
    expect(rankPlan(rows, 'id-B', { to: 'above', otherId: 'id-C' })).toBeNull();
    expect(rankPlan(rows, 'id-B', { to: 'below', otherId: 'id-A' })).toBeNull();
    expect(rankPlan(rows, 'id-B', { to: 'up', by: 0 })).toBeNull();
    expect(rankPlan(rows, 'id-missing', { to: 'top' })).toBeNull();
    expect(rankPlan(rows, 'id-B', { to: 'above', otherId: 'id-missing' })).toBeNull();
  });

  it('gives the undo placement that puts it back', () => {
    // The row's old next row, or last when it was last.
    expect(rankPlan(rows, 'id-B', { to: 'bottom' })?.undoPlacement).toEqual(before('C'));
    expect(rankPlan(rows, 'id-A', { to: 'bottom' })?.undoPlacement).toEqual(before('B'));
    expect(rankPlan(rows, 'id-E', { to: 'top' })?.undoPlacement).toEqual(last);
  });
});
