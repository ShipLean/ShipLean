import {
  DndContext,
  DragOverlay,
  PointerSensor,
  pointerWithin,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core';
import { useMemo, useRef, useState, type ReactNode } from 'react';
import type { UserRef } from '../../../api/refs';
import type { CardDto } from '../../../api/views';
import { TaskCardFace } from '../cards/TaskCardFace';
import type { TaskboardGrouping } from '../taskboardApi';
import type { TaskboardGridView } from '../taskboardGridView';
import type { TaskboardScope } from '../taskboardScope';
import { dropRequest, keyboardTarget, pickUpRequest, type DropTarget } from './moveRequests';
import { undoOf } from './applyMove';
import { TaskboardMovesContext, type TaskboardMoves } from './TaskboardMovesContext';
import { useTaskboardMove } from './useTaskboardMove';

// A card under the pointer wins over the cell around it: dropping on a card puts the dragged one above it.
const cardFirst: CollisionDetection = (args) => {
  const hits = pointerWithin(args);
  const onCard = hits.find((hit) => String(hit.id).startsWith('card:'));
  return onCard ? [onCard] : hits;
};

// Cards move with Alt + arrow keys, not with dnd-kit's keyboard sensor, so its default "press space bar" text would be wrong.
const ACCESSIBILITY = {
  screenReaderInstructions: {
    draggable: 'Press Alt with an arrow key to move this card to another column or row.',
  },
};

interface DragData {
  card: CardDto;
  rowId: string;
}
interface DropData {
  kind: 'card' | 'cell';
  rowId: string;
  stateId: string;
  cardId?: string;
}

/** Drag and drop, Alt + arrows and Pick up for the cards inside it; the page wraps its grids in it once, so a card can move between them. A move shows at once and is sent to the scope's moves endpoint. */
export function TaskboardDnd({
  projectId,
  scope,
  view,
  grouping,
  me,
  onLanded,
  children,
}: {
  projectId: string;
  scope: TaskboardScope;
  view: TaskboardGridView;
  grouping: TaskboardGrouping;
  /** The signed-in user; Pick up assigns them. */
  me: UserRef | undefined;
  /** A move landed in this row: the board opens it. */
  onLanded: (rowId: string) => void;
  children: ReactNode;
}) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }));
  const send = useTaskboardMove(projectId, scope, grouping);
  const [dragging, setDragging] = useState<CardDto | null>(null);
  const focusKey = useRef<string | null>(null);
  const startId = view.columns.find((column) => column.state.category === 'inProgress')?.state.id;

  const moves = useMemo<TaskboardMoves>(() => {
    function drop(card: CardDto, fromRowId: string, to: DropTarget) {
      const request = dropRequest(card, grouping, fromRowId, to);
      if (!request) return;
      send({
        request,
        applied: {
          taskId: card.id,
          stateId: to.stateId,
          rowId: to.rowId,
          beforeItemId: request.beforeItemId,
        },
        undo: undoOf(card, fromRowId),
        key: card.key,
      });
      onLanded(to.rowId);
    }
    return {
      drop,
      pickUp:
        me === undefined || startId === undefined
          ? null
          : (card, rowId) => {
              const landsIn = grouping === 'stories' ? rowId : me.id;
              send({
                request: pickUpRequest(card, startId, me.id),
                applied: {
                  taskId: card.id,
                  stateId: startId,
                  rowId: landsIn,
                  beforeItemId: null,
                  assignee: me,
                },
                undo: undoOf(card, rowId),
                key: card.key,
              });
              onLanded(landsIn);
            },
      keyMove: (card, rowId, key) => {
        const to = keyboardTarget(view, grouping, card, rowId, key);
        if (!to) return key.startsWith('Arrow');
        focusKey.current = card.key;
        drop(card, rowId, to);
        return true;
      },
      takeFocus: (cardKey) => {
        if (focusKey.current !== cardKey) return false;
        focusKey.current = null;
        return true;
      },
    };
  }, [view, grouping, me, startId, send, onLanded]);

  function onDragStart({ active }: DragStartEvent) {
    setDragging((active.data.current as DragData | undefined)?.card ?? null);
  }

  function onDragEnd({ active, over }: DragEndEvent) {
    setDragging(null);
    const from = active.data.current as DragData | undefined;
    const to = over?.data.current as DropData | undefined;
    if (!from || !to) return;
    moves.drop(from.card, from.rowId, {
      stateId: to.stateId,
      rowId: to.rowId,
      beforeItemId: to.kind === 'card' && to.cardId !== from.card.id ? (to.cardId ?? null) : null,
    });
  }

  return (
    <TaskboardMovesContext.Provider value={moves}>
      <DndContext
        accessibility={ACCESSIBILITY}
        sensors={sensors}
        collisionDetection={cardFirst}
        onDragStart={onDragStart}
        onDragEnd={onDragEnd}
        onDragCancel={() => setDragging(null)}
      >
        {children}
        <DragOverlay dropAnimation={null}>
          {dragging ? (
            <TaskCardFace
              card={dragging}
              view={view}
              showParent={grouping === 'people'}
              preview
              style={{ cursor: 'grabbing' }}
            />
          ) : null}
        </DragOverlay>
      </DndContext>
    </TaskboardMovesContext.Provider>
  );
}
