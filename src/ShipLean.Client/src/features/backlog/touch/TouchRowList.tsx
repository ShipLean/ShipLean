import { useMemo, type ReactNode } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import { useTouchSelectMode } from './selectMode';
import { TouchRow } from './TouchRow';

// The select box in a row's handle slot while Select mode is on: the whole slot is its hit area.
function TouchSelectBox({ row }: { row: BacklogRowDto }) {
  const selection = useSelection();
  return (
    <label className="flex size-full min-h-[var(--touch-target)] min-w-[var(--touch-target)] cursor-pointer items-center justify-center">
      <input
        type="checkbox"
        data-testid={ids.touchRowSelect}
        data-row-id={row.id}
        checked={selection.has(row.id)}
        onChange={() => selection.toggle(row.id)}
        aria-label={`Select ${row.key}`}
        className="size-4 accent-[var(--color-accent)]"
      />
    </label>
  );
}

/**
 * The ranked rows at touch (900px and below), one TouchRow each, in place of the List table. `handles` is false while
 * searching, filtering or selecting (the screen computes it); with it on, `renderHandle` fills each row's handle slot.
 * Without `renderHandle`, or with handles off, the slot shows the rank, or the select box while selecting.
 */
export function TouchRowList({
  rows,
  projectKey,
  handles,
  renderHandle,
  flashing,
  filtered,
  searched = '',
  addRow,
}: {
  rows: BacklogRowDto[];
  projectKey: string;
  handles: boolean;
  renderHandle?: (row: BacklogRowDto) => ReactNode;
  // Ids of the rows a teammate just changed.
  flashing: ReadonlySet<string>;
  // A filter, search or check queue is on: an empty list means nothing matches it.
  filtered: boolean;
  // What the search typed, when one is on: an empty list names it.
  searched?: string;
  // The inline new-item row, shown first while + New is open.
  addRow?: ReactNode;
}) {
  const { selecting } = useTouchSelectMode();
  const rowIds = useMemo(() => rows.map((row) => row.id), [rows]);
  useSelection(rowIds);
  const handleOf = (row: BacklogRowDto): ReactNode => {
    if (selecting) return <TouchSelectBox row={row} />;
    return handles && renderHandle ? renderHandle(row) : undefined;
  };
  let emptyText = `Nothing matches "${searched}"`;
  if (searched === '') emptyText = filtered ? 'Nothing matches this filter' : 'Nothing to show';
  return (
    <div data-testid={ids.touchRowList} className="min-w-0 flex-1">
      {addRow}
      {rows.map((row) => (
        <TouchRow
          key={row.id}
          row={row}
          projectKey={projectKey}
          handle={handleOf(row)}
          flash={flashing.has(row.id)}
        />
      ))}
      {rows.length === 0 ? (
        <div
          data-testid={ids.noRows}
          className="flex h-16 items-center justify-center text-[length:var(--text-sm)] text-ink-muted"
        >
          {emptyText}
        </div>
      ) : null}
    </div>
  );
}
