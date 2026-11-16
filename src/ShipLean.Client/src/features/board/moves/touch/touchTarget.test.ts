import { describe, expect, it } from 'vitest';
import { touchTarget } from './touchTarget';

// A cell holding one card, built the way the board marks them.
function board() {
  const cell = document.createElement('div');
  cell.dataset.dropTarget = 'cell:lane-1:col-2:false';
  cell.dataset.lane = 'lane-1';
  cell.dataset.column = 'col-2';
  cell.dataset.doneHalf = 'true';
  const card = document.createElement('article');
  card.dataset.dropTarget = 'card-7';
  cell.append(card);
  return { cell, card };
}

describe('touchTarget', () => {
  it('touchTarget_WhenOverCard_ShouldPlaceAboveIt', () => {
    const { card } = board();
    expect(touchTarget(card, 'card-1')).toEqual({
      lineAt: 'card-7',
      to: { laneId: 'lane-1', columnId: 'col-2', done: true, beforeItemId: 'card-7' },
    });
  });

  it('touchTarget_WhenOverEmptyCell_ShouldDropAtEnd', () => {
    const { cell } = board();
    expect(touchTarget(cell, 'card-1')).toEqual({
      lineAt: 'cell:lane-1:col-2:false',
      to: { laneId: 'lane-1', columnId: 'col-2', done: true, beforeItemId: null },
    });
  });

  it('touchTarget_WhenOverTheDraggedCardOrNothing_ShouldBeNoMove', () => {
    const { card } = board();
    expect(touchTarget(card, 'card-7')).toBeNull();
    expect(touchTarget(null, 'card-7')).toBeNull();
  });
});
