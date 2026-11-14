import { useDraggable, useDroppable } from '@dnd-kit/core';
import { useCallback, type CSSProperties } from 'react';
import type { CardDto } from '@shiplean/core/api/views';
import { useTouchShell } from '../../../touch/useTouchShell';

/**
 * A task card that can be dragged and dropped on (a card dropped on it goes above it). `rowId` is the row the card sits in.
 * The card stays where it is, dimmed, while it is dragged: the copy that follows the pointer is the drag overlay's.
 */
export function useCardDrag(card: CardDto, rowId: string) {
  // At touch widths the card is not dragged by the pointer (a swipe on it scrolls the pager); the card's ⋯ sheet moves it.
  const { touch } = useTouchShell();
  const drag = useDraggable({ id: card.id, data: { card, rowId }, disabled: touch });
  const drop = useDroppable({
    id: `card:${card.id}`,
    data: { kind: 'card', rowId, stateId: card.state.id, cardId: card.id },
  });
  const { setNodeRef: dragRef } = drag;
  const { setNodeRef: dropRef } = drop;
  const ref = useCallback(
    (node: HTMLElement | null) => {
      dragRef(node);
      dropRef(node);
    },
    [dragRef, dropRef],
  );
  const style: CSSProperties = {
    opacity: drag.isDragging ? 0.4 : undefined,
    touchAction: touch ? undefined : 'none',
  };
  return {
    ref,
    style,
    attributes: drag.attributes,
    listeners: drag.listeners,
    dragging: drag.isDragging,
  };
}
