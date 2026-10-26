import { useCallback, type KeyboardEvent } from 'react';
import type { Place } from '../moves/applyMove';
import { useBoardMoves } from '../moves/BoardMovesContext';
import { useCardDrag } from '../moves/useCardDrag';
import { Card, type CardProps } from './Card';
import { rememberPlace, slideIn } from './slide';

/** A card the pointer drags and Alt + arrow keys move. */
export function DraggableCard({ place, ...props }: CardProps & { place: Place }) {
  const { card } = props;
  const moves = useBoardMoves();
  const drag = useCardDrag(card, place);
  const { ref: dragRef } = drag;
  // A card that lands in another cell is a new element: it slides in from where the old one stood, and takes the
  // focus back after a keyboard move.
  const ref = useCallback(
    (node: HTMLElement | null) => {
      dragRef(node);
      if (!node) return undefined;
      slideIn(card.id, node);
      // The old element is still focused when the board re-renders: only the one that replaces it takes the focus.
      if (node !== document.activeElement && moves.takeFocus(card.key)) node.focus();
      return () => {
        rememberPlace(card.id, node);
        dragRef(null);
      };
    },
    [dragRef, moves, card.id, card.key],
  );
  function onKeyDown(event: KeyboardEvent) {
    if (event.altKey && moves.keyMove(card.id, card.key, event.key)) event.preventDefault();
  }
  return (
    <Card
      {...props}
      dropBefore={drag.dropBefore}
      onMoveToDone={() => moves.finish(card.id)}
      frame={{
        ref,
        ...drag.attributes,
        ...drag.listeners,
        role: 'group',
        'aria-label': `${card.title}, ${card.state.name}`,
        onKeyDown,
        style: drag.style,
      }}
    />
  );
}
