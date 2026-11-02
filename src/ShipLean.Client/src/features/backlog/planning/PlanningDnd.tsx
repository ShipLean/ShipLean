import { useEffect, useRef, useState, type ReactNode, type RefObject } from 'react';
import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  pointerWithin,
  PointerSensor,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
  type DragMoveEvent,
  type DragOverEvent,
} from '@dnd-kit/core';
import { sortableKeyboardCoordinates } from '@dnd-kit/sortable';
import type { BacklogRowDto, BacklogSectionDto } from '../../../api/views';
import { DragBadge } from '../drag/DragBadge';
import {
  dropByKey,
  dropOnRow,
  dropOnSection,
  type DropLine,
  sectionOfDropId,
  type PlanningDrop,
} from './planningTarget';
import { useRowFlip } from './useRowFlip';

/** What the sections need from the drag: the table to attach, the line to draw and the keyboard move. */
export interface PlanningDndState {
  tableRef: RefObject<HTMLTableElement | null>;
  dropLine: DropLine | null;
  onKeyMove: (row: BacklogRowDto, direction: 'up' | 'down') => void;
}

// A pointer over a row or header drops there; with the keyboard (no pointer) the nearest one does.
const collisions: CollisionDetection = (args) => {
  const within = pointerWithin(args);
  return within.length > 0 ? within : closestCenter(args);
};

// Whether a row was put down on the lower half of the row `over`. With a pointer: where the pointer is against the
// row's place in the table (offsets, which sliding rows and a scrolled page leave alone). From the keyboard: whether
// the row came from above, so moving down puts it after the row it lands on, as in the list.
function droppedBelow(
  table: HTMLTableElement | null,
  active: DragEndEvent['active'],
  over: NonNullable<DragEndEvent['over']>,
  pointerY: number | null,
): boolean {
  const row = table?.querySelector<HTMLElement>(`[data-row-id="${String(over.id)}"]`);
  if (table && row && pointerY !== null) {
    const inTable = pointerY - table.getBoundingClientRect().top - table.clientTop;
    return inTable > row.offsetTop + row.offsetHeight / 2;
  }
  const from = active.rect.current.initial;
  return from !== null && from.top < over.rect.top;
}

// Planning's drag and drop: sensors, collisions, the drop line and what a drop does. A row is dragged by its grip onto
// another row (just above it, or just below when dropped on its lower half) or onto a section header. The sections
// are `children`, given the table ref, the line and the keyboard move.
export function PlanningDnd({
  sections,
  filtering,
  onMove,
  children,
}: {
  sections: BacklogSectionDto[];
  // A filter, search or check queue is on: moving is off.
  filtering: boolean;
  onMove: (row: BacklogRowDto, drop: PlanningDrop) => void;
  children: (dnd: PlanningDndState) => ReactNode;
}) {
  const tableRef = useRef<HTMLTableElement>(null);
  const refocus = useRef<string | null>(null);
  // Where the pointer is now, in the page.
  const pointerY = useRef<number | null>(null);
  useEffect(() => {
    // Captured, so it is up to date before dnd-kit's own listeners read it.
    const track = (event: PointerEvent) => {
      pointerY.current = event.clientY;
    };
    window.addEventListener('pointermove', track, true);
    return () => window.removeEventListener('pointermove', track, true);
  }, []);
  useRowFlip(tableRef);
  useEffect(() => {
    if (refocus.current === null) return;
    const row = tableRef.current?.querySelector<HTMLElement>(`[data-row-id="${refocus.current}"]`);
    if (row) {
      row.focus();
      refocus.current = null;
    }
  });
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  const rows = sections.flatMap((section) => section.rows);
  // The row the dragged one would land beside, and on which edge: the line the design draws where the row will land.
  const [line, setLine] = useState<DropLine | null>(null);
  // dnd-kit reports a new `over` through onDragOver; onDragMove's own `over` can be one render behind the pointer. So
  // the target is kept from onDragOver and the edge is worked out again on every move.
  const overNow = useRef<DragOverEvent['over']>(null);
  function drawLine({ active, activatorEvent }: Pick<DragMoveEvent, 'active' | 'activatorEvent'>) {
    const over = overNow.current;
    const next =
      over === null || active.id === over.id || sectionOfDropId(String(over.id)) !== undefined
        ? null
        : {
            id: String(over.id),
            below: droppedBelow(
              tableRef.current,
              active,
              over,
              activatorEvent instanceof MouseEvent ? pointerY.current : null,
            ),
          };
    setLine((current) =>
      current?.id === next?.id && current?.below === next?.below ? current : next,
    );
  }
  function onDragOver(event: DragOverEvent) {
    overNow.current = event.over;
    drawLine(event);
  }
  function onDragEnd({ active, over, activatorEvent }: DragEndEvent) {
    setLine(null);
    overNow.current = null;
    const row = rows.find((r) => r.id === active.id);
    if (filtering || over === null || row === undefined) return;
    const overId = String(over.id);
    let drop: PlanningDrop | null;
    const overSection = sectionOfDropId(overId);
    if (overSection !== undefined) {
      drop = dropOnSection(sections, row.id, overSection);
    } else {
      drop = dropOnRow(
        sections,
        row.id,
        overId,
        droppedBelow(
          tableRef.current,
          active,
          over,
          activatorEvent instanceof MouseEvent ? pointerY.current : null,
        ),
      );
    }
    if (drop) onMove(row, drop);
  }
  function onKeyMove(row: BacklogRowDto, direction: 'up' | 'down') {
    if (filtering) return;
    const drop = dropByKey(sections, row.id, direction);
    if (drop === null) return;
    refocus.current = row.id;
    onMove(row, drop);
  }
  return (
    <DndContext
      sensors={sensors}
      collisionDetection={collisions}
      onDragOver={onDragOver}
      onDragMove={drawLine}
      onDragEnd={onDragEnd}
      onDragCancel={() => {
        setLine(null);
        overNow.current = null;
      }}
    >
      {children({ tableRef, dropLine: line, onKeyMove })}
      <DragBadge />
    </DndContext>
  );
}
