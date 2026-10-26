import { describe, expect, it } from 'vitest';
import type { BoardViewDto, RollupCountDto } from '../../../../api/views';
import { card, cell, lane, storiesBoard } from '../../ct/fixtures';
import { openBelow } from './openBelow';

const target = (columnId: string) => ({
  laneId: 'lane-default',
  columnId,
  done: false,
  beforeItemId: null,
});
const rollups = (open: number, closed = 1): RollupCountDto[] => [{ type: 'story', open, closed }];

function boardWith(held: ReturnType<typeof card>, columnId = 'col-prog'): BoardViewDto {
  const base = storiesBoard();
  return {
    ...base,
    lanes: base.lanes.map((candidate) =>
      candidate.id !== 'lane-default'
        ? candidate
        : lane({
            ...candidate,
            cells: candidate.cells.map((c) =>
              c.columnId === columnId && !c.doneHalf ? cell(columnId, [held, ...c.cards]) : c,
            ),
          }),
    ),
  };
}

describe('openBelow', () => {
  it('openBelow_WhenCardWithOpenChildrenGoesToDone_ShouldCountThem', () => {
    const held = card({ key: 'SHIP-101', rollups: rollups(3) });
    expect(openBelow(boardWith(held), 'features', held.id, target('col-done'))?.open).toBe(3);
  });

  it('openBelow_WhenEveryChildIsClosed_ShouldNotAsk', () => {
    const held = card({ key: 'SHIP-101', rollups: rollups(0, 4) });
    expect(openBelow(boardWith(held), 'features', held.id, target('col-done'))).toBeNull();
  });

  it('openBelow_WhenTheTargetIsNotTheLastColumn_ShouldNotAsk', () => {
    const held = card({ key: 'SHIP-101', rollups: rollups(3) });
    expect(openBelow(boardWith(held), 'features', held.id, target('col-new'))).toBeNull();
  });

  it('openBelow_WhenTheCardIsAlreadyInDone_ShouldNotAsk', () => {
    const held = card({ key: 'SHIP-101', rollups: rollups(3) });
    expect(
      openBelow(boardWith(held, 'col-done'), 'features', held.id, target('col-done')),
    ).toBeNull();
  });

  it('openBelow_WhenTheBoardIsStories_ShouldNotAsk', () => {
    const held = card({ key: 'SHIP-101', rollups: rollups(3) });
    expect(openBelow(boardWith(held), 'stories', held.id, target('col-done'))).toBeNull();
  });
});
