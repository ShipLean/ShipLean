import { useState, type ReactNode } from 'react';
import {
  closestCenter,
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core';
import {
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import type { BacklogRowDto } from '../../../api/views';
import { moveBeforePlan } from '../bulk/movePlan';
import { useBulkRunner } from '../bulk/useBulkRun';
import { inRankOrder, landsOnSelection } from '../planning/groupDrop';
import { useSelection } from '../selection/useSelection';
import { reorderTarget, type ReorderMove } from '../drag/reorderTarget';
import { DragBadge } from '../drag/DragBadge';
import { GroupDragContext, type GroupDrag } from '../drag/GroupDragContext';

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
  const selection = useSelection();
  const bulk = useBulkRunner();
  // The selected rows being dragged together, in rank order, else null: the row moves alone.
  const [group, setGroup] = useState<GroupDrag | null>(null);
  function onDragStart({ active, activatorEvent }: DragStartEvent) {
    // Keyboard drag stays single-row.
    if (activatorEvent instanceof KeyboardEvent) return;
    if (filtering || !selection.has(String(active.id)) || selection.count < 2) return;
    const picked = inRankOrder(selection.selectedRows(rows));
    setGroup({
      rows: picked,
      ids: new Set(picked.map((row) => row.id)),
      overSectionId: null,
      velocity: null,
    });
  }
  function onDragEnd({ active, over }: DragEndEvent) {
    const dragged = group;
    setGroup(null);
    if (filtering || over === null) return;
    if (dragged !== null) {
      // The block lands above the row it is dropped on; a row of the block is no target.
      if (!landsOnSelection(dragged.ids, String(over.id)))
        void bulk.run(moveBeforePlan(dragged.rows, String(over.id), undefined));
      return;
    }
    const move = reorderTarget(rowIds, String(active.id), String(over.id));
    const row = rows.find((r) => r.id === active.id);
    if (move && row) onReorder(row, move);
  }
  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      onDragCancel={() => setGroup(null)}
    >
      <GroupDragContext.Provider value={group}>
        <SortableContext items={rowIds} strategy={verticalListSortingStrategy}>
          {children}
        </SortableContext>
      </GroupDragContext.Provider>
      {/* Only mounted for a dragged selection: an overlay always mounted breaks the keyboard drag back. */}
      {group === null ? null : (
        <DragOverlay dropAnimation={null} style={{ width: 'auto', height: 'auto' }}>
          <DragBadge count={group.rows.length} />
        </DragOverlay>
      )}
    </DndContext>
  );
}
