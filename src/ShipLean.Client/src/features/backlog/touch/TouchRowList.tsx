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
  onOver,
}: {
  row: BacklogRowDto;
  onDrop: (rowId: string, targetId: string | null) => void;
  onLiftedChange: (lifted: boolean) => void;
  onOver: (targetId: string | null) => void;
}) {
  return (
    <TouchHandle
      rowId={row.id}
      rank={row.rank ?? ''}
      onDrop={(target) => onDrop(row.id, target)}
      onLiftedChange={onLiftedChange}
      onOver={onOver}
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
  // The row the finger is over: the lifted row lands just above it, so the insertion line sits there.
  const [overId, setOverId] = useState<string | null>(null);
  const handleOf = (row: BacklogRowDto): ReactNode => {
    if (selecting) return <TouchSelectBox row={row} />;
    if (!handles) return undefined;
    if (renderHandle) return renderHandle(row);
    if (!onHandleDrop) return undefined;
    return (
      <TouchListHandle
        row={row}
        onDrop={onHandleDrop}
        // Every handle reports its own state after each render: a row that is not lifted must not clear another's lift.
        onLiftedChange={(lifted) =>
          setLiftedId((current) => {
            if (lifted) return row.id;
            return current === row.id ? null : current;
          })
        }
        onOver={setOverId}
      />
    );
  };
  // A lifted row that left the list (a teammate's change) never reports the end of its drag: ignore ids that are gone.
  const lifted = liftedId !== null && rowIds.includes(liftedId) ? liftedId : null;
  const over = overId !== null && rowIds.includes(overId) ? overId : null;
  // The line sits above the row the finger is over, except on the lifted row itself and the row just below it, where a
  // drop changes nothing.
  const liftedIndex = lifted === null ? -1 : rowIds.indexOf(lifted);
  const showsLine = (id: string, index: number) =>
    lifted !== null && over === id && id !== lifted && index !== liftedIndex + 1;
  let emptyText = `Nothing matches "${searched}"`;
  if (searched === '') emptyText = filtered ? 'Nothing matches this filter' : 'Nothing to show';
  return (
    <div data-testid={ids.touchRowList} className="min-w-0 flex-1">
      {addRow}
      {rows.map((row, index) => (
        <div key={row.id} className={`relative ${lifted === row.id ? 'opacity-40' : ''}`}>
          {showsLine(row.id, index) ? (
            <div
              data-testid={ids.touchDropLine}
              aria-hidden="true"
              className="pointer-events-none absolute inset-x-0 -top-px z-10 h-0.5 bg-signal before:absolute before:-top-[3px] before:left-1.5 before:size-2 before:rounded-full before:bg-signal"
            />
          ) : null}
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
