import { useCallback, type KeyboardEvent } from 'react';
import type { CardDto, TaskboardViewDto } from '../../../api/views';
import { useCardDrag } from '../moves/useCardDrag';
import { useTaskboardMoves } from '../moves/TaskboardMovesContext';
import { TaskCardFace } from './TaskCardFace';

/** One task card: dragged by the pointer, moved with Alt + arrow keys, picked up with its button (unassigned cards in a Proposed state). */
export function TaskCard({
  card,
  rowId,
  view,
  showParent,
}: {
  card: CardDto;
  rowId: string;
  view: Pick<TaskboardViewDto, 'styleRules' | 'cardFields'>;
  showParent: boolean;
}) {
  const moves = useTaskboardMoves();
  const drag = useCardDrag(card, rowId);
  // A card a keyboard move just put down is a new element: it takes the focus back.
  const { ref: dragRef } = drag;
  const ref = useCallback(
    (node: HTMLElement | null) => {
      dragRef(node);
      // The old element is still focused when the board re-renders: only the one that replaces it takes the focus.
      if (node && node !== document.activeElement && moves.takeFocus(card.key)) node.focus();
    },
    [dragRef, moves, card.key],
  );
  const pickUp = card.assignee === null && card.state.category === 'proposed' ? moves.pickUp : null;
  function onKeyDown(event: KeyboardEvent) {
    if (event.altKey && moves.keyMove(card, rowId, event.key)) event.preventDefault();
  }
  return (
    <TaskCardFace
      card={card}
      view={view}
      showParent={showParent}
      articleRef={ref}
      articleProps={{ ...drag.attributes, ...drag.listeners, onKeyDown }}
      style={drag.style}
      pickUp={pickUp ? () => pickUp(card, rowId) : null}
    />
  );
}
