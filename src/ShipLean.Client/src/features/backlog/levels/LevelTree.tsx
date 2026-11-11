import { useState } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { ColumnSpec } from '../backlogApi';
import { backlogTestIds as ids } from '../testIds';
import { visibleRows } from '../tree/treeRows';
import type { LevelColumnId } from './levelColumns';
import { LevelTreeRow } from './LevelTreeRow';

// The # and Parent columns say nothing in a tree (the nesting is the parent), so the table leaves them out.
const HIDDEN_IN_TREE: readonly LevelColumnId[] = ['rank', 'parent'];
const widths: Partial<Record<LevelColumnId, string>> = {
  key: '90px',
  state: '120px',
  points: '110px',
  progress: '240px',
};

// The Features backlog in Tree mode: Epics with their Features and nested Features, then No parent. Every row with rows under
// it folds; everything starts open. Nothing drags here, as in List mode, and + New Feature opens its dialog, so there is no new-item row.
export function LevelTree({
  rows,
  columns,
  specs,
  projectKey,
  filtered,
}: {
  rows: BacklogRowDto[];
  columns: readonly LevelColumnId[];
  specs: readonly ColumnSpec[];
  projectKey: string;
  filtered: boolean;
}) {
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const shownColumns = columns.filter((c) => !HIDDEN_IN_TREE.includes(c));
  function toggle(id: string) {
    const next = new Set(collapsed);
    if (!next.delete(id)) next.add(id);
    setCollapsed(next);
  }
  // A row has rows under it when the next row in the whole list is deeper.
  const parents = new Set(
    rows.filter((row, i) => (rows[i + 1]?.depth ?? -1) > row.depth).map((row) => row.id),
  );
  return (
    <div data-testid={ids.list} className="flex-1 overflow-auto">
      <table className="w-full border-collapse">
        <thead>
          <tr className="h-8 border-b border-edge text-left text-[length:var(--text-xs)] text-ink-muted">
            {shownColumns.map((column) => (
              <th
                key={column}
                data-testid={ids.columnHeader}
                data-column={column}
                className={`px-2 font-semibold ${column === 'points' ? 'text-right' : ''}`}
                style={{ width: widths[column] }}
              >
                {specs.find((c) => c.id === column)?.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {visibleRows(rows, collapsed).map((row) => (
            <LevelTreeRow
              key={row.id}
              row={row}
              columns={shownColumns}
              projectKey={projectKey}
              open={parents.has(row.id) ? !collapsed.has(row.id) : undefined}
              onToggle={() => toggle(row.id)}
            />
          ))}
          {rows.length === 0 ? (
            <tr>
              <td
                colSpan={shownColumns.length}
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
