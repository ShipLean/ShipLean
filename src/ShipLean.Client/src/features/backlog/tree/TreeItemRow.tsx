import { useDraggable, useDroppable } from '@dnd-kit/core';
import type { BacklogRowDto } from '../../../api/views';
import type { BacklogColumnId } from '../backlogApi';
import { Grip } from '../drag/Grip';
import { ListCell } from '../list/ListCell';
import { backlogTestIds as ids } from '../testIds';

// A story or bug in the tree: a grip, then its cells, the title indented to its depth. Dropping another story on it puts
// that story just above this one, under this one's parent.
export function TreeItemRow({
  row,
  columns,
  projectKey,
  dragDisabled,
  flash,
}: {
  row: BacklogRowDto;
  columns: readonly BacklogColumnId[];
  projectKey: string;
  dragDisabled: boolean;
  flash: boolean;
}) {
  const closed = row.state?.category === 'completed';
  const drag = useDraggable({ id: row.id, data: { row }, disabled: dragDisabled });
  const drop = useDroppable({ id: row.id, data: { row } });
  const over = drop.isOver && drop.active !== null && drop.active.id !== row.id;
  const moving = drag.transform
    ? `translate3d(${drag.transform.x}px, ${drag.transform.y}px, 0)`
    : undefined;
  return (
    <tr
      ref={(node) => {
        drag.setNodeRef(node);
        drop.setNodeRef(node);
      }}
      data-testid={ids.row}
      data-key={row.key}
      data-flash={flash}
      data-over={over}
      style={{
        transform: moving,
        opacity: drag.isDragging ? 0.4 : 1,
        boxShadow: over ? 'inset 0 2px 0 var(--color-signal)' : undefined,
      }}
      className={`h-9 border-b border-edge text-[length:var(--text-sm)] transition-colors duration-700 ${flash ? 'bg-signal-soft' : ''} ${closed ? 'text-ink-muted' : 'text-ink'}`}
    >
      <td className="w-6 px-1 text-center">
        <Grip
          itemKey={row.key}
          disabled={dragDisabled}
          buttonRef={drag.setActivatorNodeRef}
          {...drag.attributes}
          {...drag.listeners}
        />
      </td>
      {columns.map((column) => (
        <td key={column} className={`px-2 ${column === 'points' ? 'text-right' : ''}`}>
          {column === 'title' ? (
            <span className="flex items-center" style={{ paddingLeft: row.depth * 18 }}>
              <ListCell row={row} column={column} projectKey={projectKey} />
            </span>
          ) : (
            <ListCell row={row} column={column} projectKey={projectKey} />
          )}
        </td>
      ))}
    </tr>
  );
}
