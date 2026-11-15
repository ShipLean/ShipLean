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
import type { UserRef } from '@shiplean/core/api/refs';
import type { CardDto } from '@shiplean/core/api/views';
import { TaskCardFace } from '../cards/TaskCardFace';
import type { MoveMethod, TaskboardGrouping, TaskboardMoveRequest } from '../taskboardApi';
import type { TaskboardGridView } from '../taskboardGridView';
import type { TaskboardScope } from '../taskboardScope';
import {
  assignRequest,
  dropRequest,
  keyboardTarget,
  pickUpRequest,
  reparentRequest,
  type DropTarget,
} from './moveRequests';
import { UNASSIGNED, UNPARENTED, undoOf } from './applyMove';
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
    function sendMove(
      card: CardDto,
      fromRowId: string,
      request: TaskboardMoveRequest,
      applied: { stateId: string; rowId: string; assignee?: UserRef | null },
    ) {
      send({
        request,
        applied: { taskId: card.id, beforeItemId: request.beforeItemId, ...applied },
        undo: undoOf(card, fromRowId),
        key: card.key,
      });
      onLanded(applied.rowId);
    }
    function drop(card: CardDto, fromRowId: string, to: DropTarget, method: MoveMethod) {
      const request = dropRequest(card, grouping, fromRowId, to, method);
      if (!request) return;
      sendMove(card, fromRowId, request, { stateId: to.stateId, rowId: to.rowId });
    }
    function reparent(
      card: CardDto,
      fromRowId: string,
      storyId: string | null,
      method: MoveMethod,
    ) {
      // Grouped by people the card stays in its person's row: the refetch shows its new story.
      const rowId = grouping === 'stories' ? (storyId ?? UNPARENTED) : fromRowId;
      sendMove(card, fromRowId, reparentRequest(card, storyId, method), {
        stateId: card.state.id,
        rowId,
      });
    }
    function assign(card: CardDto, fromRowId: string, person: UserRef | null, method: MoveMethod) {
      const rowId = grouping === 'stories' ? fromRowId : (person?.id ?? UNASSIGNED);
      sendMove(card, fromRowId, assignRequest(card, person?.id ?? null, method), {
        stateId: card.state.id,
        rowId,
        assignee: person,
      });
    }
    return {
      drop,
      reparent,
      assign,
      view,
      grouping,
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
        drop(card, rowId, to, 'keyboard');
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
    moves.drop(
      from.card,
      from.rowId,
      {
        stateId: to.stateId,
        rowId: to.rowId,
        beforeItemId: to.kind === 'card' && to.cardId !== from.card.id ? (to.cardId ?? null) : null,
      },
      'drag',
    );
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
