import { useContext } from 'react';
import { BoardPagerContext } from '../../grid/touch/BoardPagerContext';
import { useOptionalCardActions } from '../../cards/touch/CardActionsContext';
import { useBoardMoves } from '../BoardMovesContext';
import { useTouchShell } from '../../../../touch/useTouchShell';
import { useLongPressDrag } from '../../../../touch/useLongPressDrag';
import { useTouchOver } from './TouchOverContext';
import { touchTarget } from './touchTarget';

/**
 * A card that lifts after a long press on the phone board (K6): the drop goes through `BoardMoves` like a mouse drop.
 * `enabled` is false above 900px and outside the phone board, where dnd-kit moves cards instead.
 */
export function useTouchCardDrag(cardId: string) {
  const { touch } = useTouchShell();
  const pager = useContext(BoardPagerContext);
  const actions = useOptionalCardActions();
  const moves = useBoardMoves();
  const { setOver } = useTouchOver();
  const enabled = touch && pager !== null;
  const drag = useLongPressDrag({
    onLift: () => setOver(null),
    onMove: (target) => setOver(touchTarget(target, cardId)?.lineAt ?? null),
    onDrop: (target) => {
      setOver(null);
      const hit = touchTarget(target, cardId);
      if (hit) moves.drop(cardId, hit.to, 'touch-drag');
    },
    onTapWithoutMove: () => actions?.open(cardId),
    onEdge: (dir) => {
      setOver(null);
      pager?.turn(dir);
    },
  });
  return { enabled, ...drag };
}
