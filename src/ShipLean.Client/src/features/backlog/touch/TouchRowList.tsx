import { useMemo, useState, type ReactNode } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import { useTouchSelectMode } from './selectMode';
import { TouchHandle } from './TouchHandle';
import { TouchSelectBox } from './TouchSelectBox';
import { TouchRow } from './TouchRow';
import { TouchRowGhost } from './TouchRowGhost';

// The drag handle in a row's slot: only it drags, the row fades while lifted, and a chip with the row follows the finger.
function TouchListHandle({
  row,
  onDrop,
  onLiftedChange,
}: {
  row: BacklogRowDto;
  onDrop: (rowId: string, targetId: string | null) => void;
  onLiftedChange: (lifted: boolean) => void;
}) {
  return (
    <TouchHandle
      rowId={row.id}
      rank={row.rank ?? ''}
      onDrop={(target) => onDrop(row.id, target)}
      onLiftedChange={onLiftedChange}
      ghost={<TouchRowGhost row={row} />}
    />
  );
}

/**
 * The ranked rows at touch (900px and below), one TouchRow each, in place of the List table. `handles` is false while
 * searching, filtering or selecting (the screen computes it); with it on, `renderHandle` fills each row's handle slot.
 * `onHandleDrop(rowId, targetId)` fills the slot with the TouchHandle (what the Stories List passes, from
 * useTouchReorder().drop); `renderHandle` overrides it. Without either, or with handles off, the slot shows the rank,
 * or the select box while selecting.
 */
export function TouchRowList({
  rows,
  projectKey,
  handles,
  renderHandle,
  onHandleDrop,
  flashing,
  filtered,
  searched = '',
  addRow,
}: {
  rows: BacklogRowDto[];
  projectKey: string;
  handles: boolean;
  renderHandle?: (row: BacklogRowDto) => ReactNode;
  onHandleDrop?: (rowId: string, targetId: string | null) => void;
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
  const [liftedId, setLiftedId] = useState<string | null>(null);
  const handleOf = (row: BacklogRowDto): ReactNode => {
    if (selecting) return <TouchSelectBox row={row} />;
    if (!handles) return undefined;
    if (renderHandle) return renderHandle(row);
    if (!onHandleDrop) return undefined;
    return (
      <TouchListHandle
        row={row}
        onDrop={onHandleDrop}
        onLiftedChange={(lifted) => setLiftedId(lifted ? row.id : null)}
      />
    );
  };
  let emptyText = `Nothing matches "${searched}"`;
  if (searched === '') emptyText = filtered ? 'Nothing matches this filter' : 'Nothing to show';
  return (
    <div data-testid={ids.touchRowList} className="min-w-0 flex-1">
      {addRow}
      {rows.map((row) => (
        <div key={row.id} className={liftedId === row.id ? 'opacity-40' : ''}>
          <TouchRow
            row={row}
            projectKey={projectKey}
            handle={handleOf(row)}
            flash={flashing.has(row.id)}
          />
        </div>
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
