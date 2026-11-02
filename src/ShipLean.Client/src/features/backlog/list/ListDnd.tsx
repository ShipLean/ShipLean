import type { ReactNode } from 'react';
import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import type { BacklogRowDto } from '../../../api/views';
import { reorderTarget, type ReorderMove } from '../drag/reorderTarget';
import { DragBadge } from '../drag/DragBadge';

// The list's drag and drop: sensors, the sortable order and what a drop does. The table is `children`.
export function ListDnd({
  rows,
  filtering,
  onReorder,
  children,
}: {
  rows: BacklogRowDto[];
  // A filter, search or check queue is on: reordering is off.
  filtering: boolean;
  onReorder: (row: BacklogRowDto, move: ReorderMove) => void;
  children: ReactNode;
}) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  const rowIds = rows.map((row) => row.id);
  function onDragEnd({ active, over }: DragEndEvent) {
    if (filtering || over === null) return;
    const move = reorderTarget(rowIds, String(active.id), String(over.id));
    const row = rows.find((r) => r.id === active.id);
    if (move && row) onReorder(row, move);
  }
  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
      <SortableContext items={rowIds} strategy={verticalListSortingStrategy}>
        {children}
      </SortableContext>
      <DragBadge />
    </DndContext>
  );
}
