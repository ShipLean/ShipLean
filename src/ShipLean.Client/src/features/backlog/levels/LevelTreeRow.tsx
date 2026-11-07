import type { BacklogRowDto } from '../../../api/views';
import { backlogTestIds as ids } from '../testIds';
import { LevelCell } from './LevelCell';
import type { LevelColumnId } from './levelColumns';
import { countUnder } from './countUnder';

const numeric: readonly LevelColumnId[] = ['points'];

// A caret that folds what is under a row, or the same width of space when nothing is.
function Caret({
  row,
  open,
  onToggle,
}: {
  row: BacklogRowDto;
  open?: boolean;
  onToggle: () => void;
}) {
  if (open === undefined) return <span className="inline-block w-5" />;
  return (
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
  );
}

// One row of the Features tree. A Feature the view shows is a plain row; an Epic, or a Feature there only for what is under it,
// is a shaded group row with the same cells; No parent is one cell with the Features under it. The title is indented to its depth
// with a caret when rows sit under it (open undefined: nothing does).
export function LevelTreeRow({
  row,
  columns,
  projectKey,
  open,
  onToggle,
}: {
  row: BacklogRowDto;
  columns: readonly LevelColumnId[];
  projectKey: string;
  open?: boolean;
  onToggle: () => void;
}) {
  if (row.kind === 'noParentGroup') {
    const features = countUnder(row, 'feature').total;
    return (
      <tr
        data-testid={ids.treeGroup}
        data-kind={row.kind}
        data-key=""
        className="h-9 border-b border-edge bg-panel text-[length:var(--text-sm)]"
      >
        <td colSpan={columns.length}>
          <div className="flex items-center gap-1.5 px-2">
            <Caret row={row} open={open} onToggle={onToggle} />
            <span data-testid={ids.title} className="text-ink-muted">
              {row.title}
            </span>
            <span className="flex-1" />
            <span
              data-testid={ids.treeRollup}
              className="text-[length:var(--text-xs)] text-ink-muted"
            >
              {features} {features === 1 ? 'Feature' : 'Features'}
            </span>
          </div>
        </td>
      </tr>
    );
  }
  const group = row.kind === 'group';
  const closed = row.state?.category === 'completed';
  return (
    <tr
      data-testid={group ? ids.treeGroup : ids.row}
      data-kind={row.kind}
      data-key={row.key}
      className={`h-9 border-b border-edge text-[length:var(--text-sm)] ${group ? 'bg-panel' : ''} ${row.type?.id === 'epic' ? 'font-semibold' : ''} ${closed ? 'text-ink-muted' : 'text-ink'}`}
    >
      {columns.map((column) => (
        <td key={column} className={`px-2 ${numeric.includes(column) ? 'text-right' : ''}`}>
          {column === 'title' ? (
            <span className="flex items-center gap-1" style={{ paddingLeft: row.depth * 18 }}>
              <Caret row={row} open={open} onToggle={onToggle} />
              <LevelCell row={row} column={column} projectKey={projectKey} />
            </span>
          ) : (
            <LevelCell row={row} column={column} projectKey={projectKey} />
          )}
        </td>
      ))}
    </tr>
  );
}
