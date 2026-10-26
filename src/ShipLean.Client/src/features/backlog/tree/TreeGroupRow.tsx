import { useDroppable } from '@dnd-kit/core';
import type { BacklogRowDto } from '../../../api/views';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { backlogTestIds as ids } from '../testIds';
import { rollupText } from './treeRows';

// An Epic, a Feature or the No parent group: a caret to fold it, what it is and its roll-up. A story dropped on it takes it as parent (No parent clears it).
export function TreeGroupRow({
  row,
  columnCount,
  open,
  includeCompleted,
  onToggle,
}: {
  row: BacklogRowDto;
  columnCount: number;
  open: boolean;
  includeCompleted: boolean;
  onToggle: () => void;
}) {
  const { setNodeRef, isOver, active } = useDroppable({ id: row.id, data: { row } });
  const over = isOver && active !== null;
  return (
    <tr
      ref={setNodeRef}
      data-testid={ids.treeGroup}
      data-kind={row.kind}
      data-key={row.key}
      data-over={over}
      className={`h-9 border-b border-edge bg-panel text-[length:var(--text-sm)] ${over ? 'bg-signal-soft' : ''}`}
    >
      <td colSpan={columnCount}>
        <div className="flex items-center gap-1.5 px-2" style={{ paddingLeft: row.depth * 18 + 8 }}>
          <button
            type="button"
            data-testid={ids.treeCaret}
            aria-expanded={open}
            aria-label={`${open ? 'Collapse' : 'Expand'} ${row.title}`}
            className="w-5 text-ink-muted"
            onClick={onToggle}
          >
            {open ? '▼' : '▶'}
          </button>
          {row.kind === 'group' && row.type ? <TypeIcon type={row.type.id} /> : null}
          {row.kind === 'group' ? (
            <span data-testid={ids.key} className="text-ink-muted">
              {row.key}
            </span>
          ) : null}
          <span
            data-testid={ids.title}
            className={row.kind === 'group' ? 'font-semibold' : 'text-ink-muted'}
          >
            {row.title}
          </span>
          {row.state ? <StateDot category={row.state.category} name={row.state.name} /> : null}
          <span className="flex-1" />
          <span
            data-testid={ids.treeRollup}
            data-warn={row.sumWarn}
            className={`text-[length:var(--text-xs)] ${row.sumWarn ? 'text-caution' : 'text-ink-muted'}`}
          >
            {rollupText(row, includeCompleted)}
          </span>
        </div>
      </td>
    </tr>
  );
}
