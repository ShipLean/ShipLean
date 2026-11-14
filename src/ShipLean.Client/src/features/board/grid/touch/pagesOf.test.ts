import { describe, expect, it } from 'vitest';
import { cell, card, column, lane, storiesBoard } from '../../ct/fixtures';
import { pagesOf } from './pagesOf';

const NONE: ReadonlySet<string> = new Set();

describe('pagesOf', () => {
  it('pagesOf_WhenLaneEmptyInColumn_ShouldLeaveItOut', () => {
    const view = storiesBoard({
      columns: [column({ id: 'a' }), column({ id: 'b' })],
      lanes: [
        lane({
          id: 'one',
          kind: 'default',
          cells: [cell('a', [card({ key: 'X-1' })]), cell('b')],
        }),
        lane({
          id: 'two',
          kind: 'criteria',
          cells: [cell('a'), cell('b', [card({ key: 'X-2' })])],
        }),
      ],
    });
    const pages = pagesOf(view, NONE);
    expect(pages[0]?.lanes.map((l) => l.lane.id)).toEqual(['one']);
    expect(pages[1]?.lanes.map((l) => l.lane.id)).toEqual(['two']);
  });

  it('pagesOf_WhenLaneCollapsed_ShouldKeepHeader', () => {
    const view = storiesBoard({
      columns: [column({ id: 'a' })],
      lanes: [
        lane({
          id: 'one',
          kind: 'default',
          cells: [cell('a', [card({ key: 'X-1' }), card({ key: 'X-2' })])],
        }),
      ],
    });
    const [page] = pagesOf(view, new Set(['one']));
    expect(page?.lanes).toHaveLength(1);
    expect(page?.lanes[0]).toMatchObject({ collapsed: true, count: 2, cells: [] });
  });

  it('pagesOf_WhenColumnSplit_ShouldStackDoingThenDone', () => {
    const view = storiesBoard({
      columns: [column({ id: 'a', split: true })],
      lanes: [
        lane({
          id: 'one',
          kind: 'default',
          cells: [
            cell('a', [card({ key: 'X-2' })], { doneHalf: true }),
            cell('a', [card({ key: 'X-1' })]),
          ],
        }),
      ],
    });
    const pages = pagesOf(view, NONE);
    expect(pages).toHaveLength(1);
    expect(pages[0]?.lanes[0]?.cells.map((c) => c.track.doneHalf)).toEqual([false, true]);
    expect(pages[0]?.lanes[0]?.cells.map((c) => c.cell?.cards[0]?.key)).toEqual(['X-1', 'X-2']);
  });
});
