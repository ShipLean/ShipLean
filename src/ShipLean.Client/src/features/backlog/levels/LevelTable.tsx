import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { ColumnSpec } from '../backlogApi';
import type { ColumnSort } from '../sort/columnSort';
import { useTouchShell } from '../../../touch/useTouchShell';
import { SortableHeader } from '../sort/SortableHeader';
import { backlogTestIds as ids } from '../testIds';
import type { LevelBacklogLevel, LevelColumnId } from './levelColumns';
import { LevelNewItemRow } from './LevelNewItemRow';
import { LevelRow } from './LevelRow';
import { TouchLevelRow } from './TouchLevelRow';

const numeric: readonly LevelColumnId[] = ['rank', 'points', 'features'];
const widths: Partial<Record<LevelColumnId, string>> = {
  rank: '48px',
  key: '90px',
  parent: '190px',
  state: '120px',
  features: '80px',
  points: '110px',
  progress: '240px',
};

// The ranked table of one level: a header, the optional inline new-item row, then one row per Feature or Epic.
export function LevelTable({
  rows,
  columns,
  specs,
  level,
  projectId,
  projectKey,
  adding,
  onCloseAdd,
  filtered,
  sort,
  onSort,
}: {
  rows: BacklogRowDto[];
  columns: readonly LevelColumnId[];
  specs: readonly ColumnSpec[];
  level: LevelBacklogLevel;
  projectId: string;
  projectKey: string;
  adding: boolean;
  onCloseAdd: () => void;
  // A filter is on: an empty list means nothing matches it.
  filtered: boolean;
  // The column the rows are sorted by (null = rank order), and what a click on a column title does.
  sort: ColumnSort;
  onSort: (column: string) => void;
}) {
  const { touch } = useTouchShell();
  if (touch) {
    // At 900px and below the level is a list of rows, not a table (SHIP-716 D1, D8).
    return (
      <div data-testid={ids.list} className="flex-1 overflow-auto">
        <div data-testid={ids.touchRowList} className="min-w-0">
          {adding ? (
            <LevelNewItemRow
              level={level}
              projectId={projectId}
              projectKey={projectKey}
              columnCount={columns.length}
              onClose={onCloseAdd}
            />
          ) : null}
          {rows.map((row) => (
            <TouchLevelRow key={row.id} row={row} projectKey={projectKey} level={level} />
          ))}
          {rows.length === 0 ? (
            <div
              data-testid={ids.noRows}
              className="flex h-16 items-center justify-center text-[length:var(--text-sm)] text-ink-muted"
            >
              {filtered ? 'Nothing matches this filter' : 'Nothing to show'}
            </div>
          ) : null}
        </div>
      </div>
    );
  }
  return (
    <div data-testid={ids.list} className="flex-1 overflow-auto">
      <table className="w-full border-collapse">
        <thead>
          <tr className="h-8 border-b border-edge text-left text-[length:var(--text-xs)] text-ink-muted">
            {columns.map((column) => (
              <SortableHeader
                key={column}
                column={column}
                sort={sort}
                onSort={onSort}
                numeric={numeric.includes(column)}
                width={widths[column]}
              >
                {specs.find((c) => c.id === column)?.label}
              </SortableHeader>
            ))}
          </tr>
        </thead>
        <tbody>
          {adding ? (
            <LevelNewItemRow
              level={level}
              projectId={projectId}
              projectKey={projectKey}
              columnCount={columns.length}
              onClose={onCloseAdd}
            />
          ) : null}
          {rows.map((row) => (
            <LevelRow key={row.id} row={row} columns={columns} projectKey={projectKey} />
          ))}
          {rows.length === 0 ? (
            <tr>
              <td
                colSpan={columns.length}
                data-testid={ids.noRows}
                className="h-16 text-center text-[length:var(--text-sm)] text-ink-muted"
              >
                {filtered ? 'Nothing matches this filter' : 'Nothing to show'}
              </td>
            </tr>
          ) : null}
        </tbody>
      </table>
    </div>
  );
}
