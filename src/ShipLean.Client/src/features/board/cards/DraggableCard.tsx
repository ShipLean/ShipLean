import { useCallback, type DragEvent, type KeyboardEvent } from 'react';
import { TouchDragGhost } from '../../../touch/TouchDragGhost';
import { useTouchCardDrag } from '../moves/touch/useTouchCardDrag';
import { useTouchOver } from '../moves/touch/TouchOverContext';
import { boardTestIds as ids } from '../testIds';
import type { Place } from '../moves/applyMove';
import { useBoardMoves } from '../moves/BoardMovesContext';
import { useCardDrag } from '../moves/useCardDrag';
import { Card, type CardProps } from './Card';
import { rememberPlace, slideIn } from './slide';

/** A card the pointer drags and Alt + arrow keys move. */
export function DraggableCard({ place, ...props }: CardProps & { place: Place }) {
  const { card } = props;
  const moves = useBoardMoves();
  const touchDrag = useTouchCardDrag(card.id);
  const { enabled, lifted } = touchDrag;
  const drag = useCardDrag(card, place, enabled);
  const { ref: dragRef } = drag;
  const { ref: touchRef } = touchDrag.props;
  const touchOver = useTouchOver().over === card.id;
  // A card that lands in another cell is a new element: it slides in from where the old one stood, and takes the
  // focus back after a keyboard move.
  const ref = useCallback(
    (node: HTMLElement | null) => {
      dragRef(node);
      if (!node) return undefined;
      const stopTouch = enabled ? touchRef(node) : undefined;
      slideIn(card.id, node);
      // The old element is still focused when the board re-renders: only the one that replaces it takes the focus.
      if (node !== document.activeElement && moves.takeFocus(card.key)) node.focus();
      return () => {
        stopTouch?.();
        rememberPlace(card.id, node);
        dragRef(null);
      };
    },
    [dragRef, touchRef, enabled, moves, card.id, card.key],
  );
  // A spread, not a key: the frame's type has no data-* attributes.
  const dropTarget = { 'data-drop-target': card.id };
  function onKeyDown(event: KeyboardEvent) {
    if (event.altKey && moves.keyMove(card.id, card.key, event.key)) event.preventDefault();
  }
  return (
    <>
      <Card
        {...props}
        dropBefore={drag.dropBefore || touchOver}
        onMoveToDone={() => moves.finish(card.id)}
        frame={{
          ref,
          ...drag.attributes,
          ...drag.listeners,
          role: 'group',
          'aria-label': `${card.title}, ${card.state.name}`,
          onKeyDown,
          // A long press on the title link would start the browser's link drag and cancel the touch: the lift is ours.
          onDragStart: enabled ? (event: DragEvent) => event.preventDefault() : undefined,
          ...dropTarget,
          style: {
            ...drag.style,
            ...(enabled ? touchDrag.props.style : undefined),
            opacity: lifted ? 0.4 : drag.style.opacity,
          },
        }}
      />
      {lifted ? (
        <TouchDragGhost x={touchDrag.x} y={touchDrag.y}>
          <div data-testid={ids.liftedCard} className="px-2 py-1.5 text-[length:var(--text-sm)]">
            <span className="text-[length:var(--text-2xs)] text-ink-muted">{card.key}</span>
            <p className="line-clamp-2 font-medium">{card.title}</p>
          </div>
        </TouchDragGhost>
      ) : null}
    </>
  );
}
