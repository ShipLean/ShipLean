import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { LongPressDragOptions } from '../../../../touch/useLongPressDrag';
import { BoardPagerContext, type BoardPager } from '../../grid/touch/BoardPagerContext';
import { BoardMovesContext, type BoardMoves } from '../BoardMovesContext';
import { useTouchCardDrag } from './useTouchCardDrag';

let options: LongPressDragOptions;
vi.mock('../../../../touch/useLongPressDrag', () => ({
  useLongPressDrag: (next: LongPressDragOptions) => {
    options = next;
    return { props: { ref: () => () => {}, style: {} }, lifted: false, x: 0, y: 0 };
  },
}));
vi.mock('../../../../touch/useTouchShell', () => ({
  useTouchShell: () => ({ touch: true, viewport: 'phone' }),
}));

const turned: string[] = [];
const dropped: unknown[][] = [];
const pager: BoardPager = { columnId: 'c', turn: (dir) => turned.push(dir), goTo: () => {} };
const moves = { drop: (...args: unknown[]) => dropped.push(args) } as unknown as BoardMoves;

function Probe() {
  useTouchCardDrag('card-1');
  return null;
}

function mount() {
  const root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <BoardMovesContext.Provider value={moves}>
        <BoardPagerContext.Provider value={pager}>
          <Probe />
        </BoardPagerContext.Provider>
      </BoardMovesContext.Provider>,
    ),
  );
}

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  turned.length = 0;
  dropped.length = 0;
});

describe('useTouchCardDrag', () => {
  it('useTouchCardDrag_WhenEdgeFired_ShouldTurnPagerThatWay', () => {
    mount();
    options.onEdge?.('right');
    options.onEdge?.('left');
    expect(turned).toEqual(['right', 'left']);
  });

  it('useTouchCardDrag_WhenDroppedOnCell_ShouldSendTouchDragMethod', () => {
    mount();
    const cell = document.createElement('div');
    Object.assign(cell.dataset, { dropTarget: 'k', lane: 'l', column: 'c', doneHalf: 'false' });
    options.onDrop(cell, { x: 0, y: 0 });
    expect(dropped).toEqual([
      ['card-1', { laneId: 'l', columnId: 'c', done: false, beforeItemId: null }, 'touch-drag'],
    ]);
  });
});
