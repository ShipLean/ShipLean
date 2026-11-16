import { useState } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { useTouchShell } from '../../../touch/useTouchShell';
import type { ColumnSpec } from '../backlogApi';
import { backlogTestIds as ids } from '../testIds';
import { TouchTreeGroup } from '../tree/touch/TouchTree';
import { treeParents } from '../tree/touch/treePath';
import { visibleRows } from '../tree/treeRows';
import { countUnder } from './countUnder';
import type { LevelColumnId } from './levelColumns';
import { LevelTreeRow } from './LevelTreeRow';
import { TouchLevelRow } from './TouchLevelRow';

// The # and Parent columns say nothing in a tree (the nesting is the parent), so the table leaves them out.
const HIDDEN_IN_TREE: readonly LevelColumnId[] = ['rank', 'parent'];
const widths: Partial<Record<LevelColumnId, string>> = {
  key: '90px',
  state: '120px',
  points: '110px',
  progress: '240px',
};

function LevelTouchCaret({
  row,
  open,
  onToggle,
}: {
  row: BacklogRowDto;
  open: boolean;
  onToggle: () => void;
}) {
  return (
    <button
      type="button"
      data-testid={ids.treeCaret}
      aria-expanded={open}
      aria-label={`${open ? 'Collapse' : 'Expand'} ${row.title}`}
      className="flex min-h-[var(--touch-target)] min-w-[var(--touch-target)] flex-none items-center justify-center border-b border-edge text-ink-muted"
      onClick={onToggle}
    >
      {open ? '▼' : '▶'}
    </button>
  );
}

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
  const { touch } = useTouchShell();
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
  if (touch) {
    // At 900px and below: path headers for Epics, group Features and No parent, a TouchLevelRow per Feature, none indented.
    // A Feature with rows under it gets a caret of its own at the right of the row, so every title starts at the same left edge.
    return (
      <div data-testid={ids.touchTree} className="min-w-0 flex-1">
        <div data-testid={ids.list}>
          {visibleRows(rows, collapsed).map((row) => {
            const open = parents.has(row.id) ? !collapsed.has(row.id) : undefined;
            if (row.kind === 'item') {
              const rowEl = <TouchLevelRow row={row} projectKey={projectKey} level="features" />;
              if (open === undefined) return <div key={row.id}>{rowEl}</div>;
              return (
                <div key={row.id} className="flex items-stretch">
                  <div className="min-w-0 flex-1">{rowEl}</div>
                  <LevelTouchCaret row={row} open={open} onToggle={() => toggle(row.id)} />
                </div>
              );
            }
            const features = countUnder(row, 'feature').total;
            return (
              <TouchTreeGroup
                key={row.id}
                row={row}
                parents={treeParents(rows, row)}
                open={!collapsed.has(row.id)}
                rollup={`${features} ${features === 1 ? 'Feature' : 'Features'}`}
                onToggle={() => toggle(row.id)}
              />
            );
          })}
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
