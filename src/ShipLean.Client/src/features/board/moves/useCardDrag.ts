import { useDndContext, useDraggable, useDroppable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { useCallback, type CSSProperties } from 'react';
import type { CardDto } from '../../../api/views';
import type { Place } from './applyMove';

/** A card that can be dragged and dropped on (a card dropped on it goes above it). `place` is where the card sits. */
export function useCardDrag(card: CardDto, place: Place) {
  const drag = useDraggable({ id: card.id, data: { itemId: card.id } });
  const drop = useDroppable({
    id: `card:${card.id}`,
    data: { kind: 'card', cardId: card.id, ...place },
  });
  const { active } = useDndContext();
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
    transform: CSS.Translate.toString(drag.transform),
    zIndex: drag.isDragging ? 20 : undefined,
    opacity: drag.isDragging ? 0.4 : undefined,
    touchAction: 'none',
  };
  // A dragged card over another puts it above that one (over itself it stays put).
  const dropBefore = drop.isOver && active?.id !== card.id;
  return { ref, style, dropBefore, attributes: drag.attributes, listeners: drag.listeners };
}
