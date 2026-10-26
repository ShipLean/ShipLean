import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import type { BacklogRowDto } from '../../../api/views';
import type { BacklogColumnId } from '../backlogApi';
import { Grip } from '../drag/Grip';
import { backlogTestIds as ids } from '../testIds';
import { ListCell } from './ListCell';

const numeric: readonly BacklogColumnId[] = ['rank', 'points'];

// One item of the list: a grip to drag it by, then its cells. Closed items are muted; a teammate's change flashes the row.
export function ListRow({
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
  const {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: row.id, disabled: dragDisabled });
  return (
    <tr
      ref={setNodeRef}
      data-testid={ids.row}
      data-key={row.key}
      data-flash={flash}
      style={{
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.4 : 1,
      }}
      className={`h-9 border-b border-edge text-[length:var(--text-sm)] transition-colors duration-700 ${flash ? 'bg-signal-soft' : ''} ${closed ? 'text-ink-muted' : 'text-ink'}`}
    >
      <td className="w-6 px-1 text-center">
        <Grip
          itemKey={row.key}
          disabled={dragDisabled}
          buttonRef={setActivatorNodeRef}
          {...attributes}
          {...listeners}
        />
      </td>
      {columns.map((column) => (
        <td key={column} className={`px-2 ${numeric.includes(column) ? 'text-right' : ''}`}>
          <ListCell row={row} column={column} projectKey={projectKey} />
        </td>
      ))}
    </tr>
  );
}
