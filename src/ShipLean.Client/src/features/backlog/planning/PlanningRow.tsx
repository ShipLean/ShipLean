import type { KeyboardEvent } from 'react';
import { useDraggable, useDroppable } from '@dnd-kit/core';
import type { BacklogRowDto } from '../../../api/views';
import type { BacklogColumnId } from '../backlogApi';
import { Grip } from '../drag/Grip';
import { ListCell } from '../list/ListCell';
import { backlogTestIds as ids } from '../testIds';

const numeric: readonly BacklogColumnId[] = ['rank', 'points'];

// One item of a section: the forecast cell (the sprint the cut line below it leads into), a grip to drag it by, then
// the list's cells. A cut line is the row's bottom border. Alt+Up and Alt+Down on the focused row move it one place.
export function PlanningRow({
  row,
  sectionId,
  columns,
  projectKey,
  mark,
  forecast,
  flash,
  dropEdge,
  onKeyMove,
}: {
  row: BacklogRowDto;
  sectionId: string;
  columns: readonly BacklogColumnId[];
  projectKey: string;
  mark: string | undefined;
  forecast: boolean;
  // A teammate just changed this row.
  flash: boolean;
  // The row a dragged one is about to land above ('top') or below ('bottom'): the accent line is drawn on that edge.
  dropEdge: 'top' | 'bottom' | null;
  onKeyMove: (row: BacklogRowDto, direction: 'up' | 'down') => void;
}) {
  const closed = row.state?.category === 'completed';
  const {
    attributes,
    listeners,
    setNodeRef: dragRef,
    setActivatorNodeRef,
    isDragging,
  } = useDraggable({ id: row.id });
  const { setNodeRef: dropRef } = useDroppable({ id: row.id });
  function onKeyDown(event: KeyboardEvent<HTMLTableRowElement>) {
    if (!event.altKey) return;
    if (event.key !== 'ArrowUp' && event.key !== 'ArrowDown') return;
    event.preventDefault();
    onKeyMove(row, event.key === 'ArrowUp' ? 'up' : 'down');
  }
  return (
    <tr
      ref={(node) => {
        dragRef(node);
        dropRef(node);
      }}
      tabIndex={0}
      onKeyDown={onKeyDown}
      data-testid={ids.row}
      data-key={row.key}
      data-row-id={row.id}
      data-section={sectionId}
      data-flash={flash}
      data-over={dropEdge !== null}
      data-edge={dropEdge ?? undefined}
      style={{ opacity: isDragging ? 0.4 : 1 }}
      className={`h-9 data-[edge=bottom]:[&>td]:shadow-[inset_0_-2px_0_var(--color-accent)] data-[edge=top]:[&>td]:shadow-[inset_0_2px_0_var(--color-accent)] text-[length:var(--text-sm)] transition-colors duration-700 ${flash ? 'bg-signal-soft' : ''} ${mark ? 'border-b-2 border-signal' : 'border-b border-edge'} ${closed ? 'text-ink-muted' : 'text-ink'}`}
    >
      {forecast ? (
        <td className="w-[92px] px-2 text-[length:var(--text-xs)] font-semibold whitespace-nowrap text-signal">
          {mark ? <span data-testid={ids.planningForecastLine}>→ {mark}</span> : null}
        </td>
      ) : null}
      <td className="w-6 px-1 text-center">
        <Grip
          itemKey={row.key}
          disabled={false}
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
