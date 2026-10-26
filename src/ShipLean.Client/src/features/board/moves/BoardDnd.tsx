import {
  DndContext,
  PointerSensor,
  pointerWithin,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
} from '@dnd-kit/core';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import type { BoardViewDto } from '../../../api/views';
import type { BoardContext } from '../grid/boardContext';
import type { DropTarget } from './applyMove';
import { BoardMovesContext, type BoardMoves } from './BoardMovesContext';
import { DoneConfirm } from './doneConfirm/DoneConfirm';
import { openBelow, type OpenBelow } from './doneConfirm/openBelow';
import { bodyOf, keyboardTarget, staysPut } from './moveRequests';
import { applyMove, locate } from './applyMove';
import { tracksOf } from '../grid/layout';
import { useBoardMove, type Sent } from './useBoardMove';

// A card under the pointer wins over the cell around it: dropping on a card puts the dragged one above it.
const cardFirst: CollisionDetection = (args) => {
  const hits = pointerWithin(args);
  const onCard = hits.find((hit) => String(hit.id).startsWith('card:'));
  return onCard ? [onCard] : hits;
};

// Cards move with the pointer or with Alt + arrows; dnd-kit's own text talks of picking one up with the space bar.
const INSTRUCTIONS =
  'Drag this card with the pointer, or hold Alt and press the left or right arrow to move it to another column, and the up or down arrow to move it within its column.';

/** A move into Done that waits for the answer: the card with open children, and the request to send on "move anyway". */
interface AskedMove {
  below: OpenBelow;
  request: Sent;
  focus: boolean;
}

interface DragData {
  itemId: string;
}
interface DropData extends DropTarget {
  kind: 'card' | 'cell';
  cardId?: string;
}

/** Drag and drop and Alt + arrows for the cards inside it. A move shows at once and is sent to the board's moves endpoint. */
export function BoardDnd({
  view,
  context,
  children,
}: {
  view: BoardViewDto;
  context: Pick<BoardContext, 'projectId' | 'boardId' | 'level'>;
  children: ReactNode;
}) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }));
  const send = useBoardMove(context.projectId, context.boardId, context.level);
  const focusKey = useRef<string | null>(null);
  // A move into Done that waits for the answer to "move it anyway?".
  const [asking, setAsking] = useState<AskedMove | null>(null);
  // The click that ends a drag lands on whatever now stands under the pointer (the card that was dragged there is a
  // new element with a link): it must not open it.
  const dragged = useRef(false);
  useEffect(() => {
    const forget = () => {
      dragged.current = false;
    };
    // Natively, not through React: the link the click lands on may already be a detached element React never hears from.
    const swallow = (event: MouseEvent) => {
      if (!dragged.current) return;
      dragged.current = false;
      event.preventDefault();
      event.stopPropagation();
    };
    document.addEventListener('pointerdown', forget, true);
    document.addEventListener('click', swallow, true);
    return () => {
      document.removeEventListener('pointerdown', forget, true);
      document.removeEventListener('click', swallow, true);
    };
  }, []);
  // The handlers read the board as it is when they run, so they stay the same across renders.
  const shown = useRef(view);
  useEffect(() => {
    shown.current = view;
  }, [view]);

  const { level } = context;
  const go = useCallback(
    (request: AskedMove['request'], focus: boolean) => {
      if (focus) focusKey.current = request.key;
      send(request);
      // The optimistic view reaches the next render a tick later: a key pressed before then moves on from here.
      shown.current = applyMove(shown.current, request.body.itemId, request.to);
    },
    [send],
  );
  const moves = useMemo<BoardMoves>(() => {
    function drop(itemId: string, to: DropTarget, key: string, focus: boolean) {
      const view = shown.current;
      if (staysPut(view, itemId, to)) return;
      const columnName = view.columns.find((column) => column.id === to.columnId)?.name ?? '';
      const below = openBelow(view, level, itemId, to);
      const request = { body: bodyOf(itemId, to), to, key, columnName, stillOpen: below?.text };
      if (below) setAsking({ below, request, focus });
      else go(request, focus);
    }
    const keyOf = (itemId: string) => locateKey(shown.current, itemId);
    return {
      drop: (itemId, to) => drop(itemId, to, keyOf(itemId), false),
      keyMove: (itemId, cardKey, key) => {
        const to = keyboardTarget(shown.current, itemId, key);
        if (!to) return key.startsWith('Arrow');
        drop(itemId, to, cardKey, true);
        return true;
      },
      finish: (itemId) => {
        const view = shown.current;
        const at = locate(view, itemId);
        const last = tracksOf(view.columns).at(-1);
        if (at && last)
          drop(
            itemId,
            {
              laneId: at.laneId,
              columnId: last.column.id,
              done: last.doneHalf,
              beforeItemId: null,
            },
            at.card.key,
            false,
          );
      },
      takeFocus: (cardKey) => {
        if (focusKey.current !== cardKey) return false;
        focusKey.current = null;
        return true;
      },
    };
  }, [go, level]);

  function onDragEnd({ active, over }: DragEndEvent) {
    const from = active.data.current as DragData | undefined;
    const to = over?.data.current as DropData | undefined;
    if (!from || !to) return;
    moves.drop(from.itemId, {
      laneId: to.laneId,
      columnId: to.columnId,
      done: to.done,
      beforeItemId: to.kind === 'card' && to.cardId !== from.itemId ? (to.cardId ?? null) : null,
    });
  }

  return (
    <BoardMovesContext.Provider value={moves}>
      <DndContext
        sensors={sensors}
        collisionDetection={cardFirst}
        accessibility={{ screenReaderInstructions: { draggable: INSTRUCTIONS } }}
        onDragStart={() => {
          dragged.current = true;
        }}
        onDragEnd={onDragEnd}
      >
        {children}
      </DndContext>
      {asking ? (
        <DoneConfirm
          projectId={context.projectId}
          level={level}
          asking={asking.below}
          onCancel={() => setAsking(null)}
          onMove={() => {
            setAsking(null);
            go(asking.request, asking.focus);
          }}
        />
      ) : null}
    </BoardMovesContext.Provider>
  );
}

function locateKey(view: BoardViewDto, itemId: string): string {
  for (const lane of view.lanes)
    for (const cell of lane.cells) {
      const found = cell.cards.find((card) => card.id === itemId);
      if (found) return found.key;
    }
  return itemId;
}
