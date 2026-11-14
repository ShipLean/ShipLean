import { useState, type ReactNode } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { StateDot } from '../../../../components/StateDot';
import { TypeIcon } from '../../../../components/TypeIcon';
import { TouchHandle } from '../../touch/TouchHandle';
import { TouchRow } from '../../touch/TouchRow';
import { backlogTestIds as ids } from '../../testIds';
import { rollupText, visibleRows } from '../treeRows';
import { treePath } from './treePath';
import { useTouchReparent } from './useTouchReparent';

// A group's header at touch: a caret at least --touch-target square, the path from its Epic down ("Checkout › Address
// autocomplete", never cut) and its roll-up. It is a drop target for a lifted handle, and lights while one is over it.
export function TouchTreeGroup({
  row,
  path,
  open,
  rollup,
  over = false,
  onToggle,
}: {
  row: BacklogRowDto;
  path: string;
  open: boolean;
  // The roll-up text on the right.
  rollup: string;
  over?: boolean;
  onToggle: () => void;
}) {
  return (
    <div
      data-testid={ids.treeGroup}
      data-kind={row.kind}
      data-key={row.key}
      data-drop-target={row.id}
      data-over={over}
      className={`flex min-h-[var(--touch-target)] items-stretch border-b border-edge text-[length:var(--text-sm)] ${over ? 'bg-signal-soft' : 'bg-panel'}`}
    >
      <button
        type="button"
        data-testid={ids.treeCaret}
        aria-expanded={open}
        aria-label={`${open ? 'Collapse' : 'Expand'} ${row.title}`}
        className="flex min-h-[var(--touch-target)] min-w-[var(--touch-target)] flex-none items-center justify-center text-ink-muted"
        onClick={onToggle}
      >
        {open ? '▼' : '▶'}
      </button>
      <div className="flex min-w-0 flex-1 flex-wrap items-center gap-x-2 gap-y-1 py-1.5 pr-3">
        {row.kind === 'group' && row.type ? <TypeIcon type={row.type.id} /> : null}
        <span
          data-testid={ids.treeGroupPath}
          className={`min-w-0 break-words ${row.kind === 'group' ? 'font-semibold' : 'text-ink-muted'}`}
        >
          {path}
        </span>
        {row.state ? <StateDot category={row.state.category} name={row.state.name} /> : null}
        <span
          data-testid={ids.treeRollup}
          data-warn={row.sumWarn}
          className={`ml-auto text-[length:var(--text-xs)] ${row.sumWarn ? 'text-caution' : 'text-ink-muted'}`}
        >
          {rollup}
        </span>
      </div>
    </div>
  );
}

// One story in the tree: a TouchRow, never indented, with its drag handle while handles are on. It fades while lifted.
function TouchTreeItem({
  row,
  projectKey,
  handle,
  flash,
  onOver,
  onDrop,
}: {
  row: BacklogRowDto;
  projectKey: string;
  handle: boolean;
  flash: boolean;
  onOver: (targetId: string | null) => void;
  onDrop: (targetId: string | null) => void;
}) {
  const [lifted, setLifted] = useState(false);
  return (
    <div className={lifted ? 'opacity-40' : ''}>
      <TouchRow
        row={row}
        projectKey={projectKey}
        flash={flash}
        handle={
          handle ? (
            <TouchHandle
              rowId={row.id}
              rank={row.rank ?? ''}
              onOver={onOver}
              onDrop={onDrop}
              onLiftedChange={setLifted}
              ghost={
                <div className="w-[min(80vw,22rem)] truncate rounded-control border border-accent bg-panel px-3 py-3 text-[length:var(--text-sm)] font-medium shadow-lg">
                  {row.key} {row.title}
                </div>
              }
            />
          ) : undefined
        }
      />
    </div>
  );
}

/**
 * The Stories tree at touch (900px and below, SHIP-716 D6): a path header per group and a TouchRow per story, none of
 * them indented. A story is lifted by its handle and dropped on a group header (or another story); the drop goes to
 * `onDrop`, TreeMode's one write. `handles` is false while filtering or selecting.
 */
export function TouchTree({
  rows,
  projectKey,
  handles,
  includeCompleted,
  flashing,
  filtered,
  addRow,
  onDrop,
}: {
  rows: BacklogRowDto[];
  projectKey: string;
  handles: boolean;
  includeCompleted: boolean;
  flashing: ReadonlySet<string>;
  filtered: boolean;
  addRow?: ReactNode;
  onDrop: (active: BacklogRowDto, over: BacklogRowDto) => void;
}) {
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const reparent = useTouchReparent(rows, onDrop);
  function toggle(id: string) {
    const next = new Set(collapsed);
    if (!next.delete(id)) next.add(id);
    setCollapsed(next);
  }
  return (
    <div data-testid={ids.touchTree} className="min-w-0 flex-1">
      <div data-testid={ids.list}>
        {addRow}
        {visibleRows(rows, collapsed).map((row) =>
          row.kind === 'item' ? (
            <TouchTreeItem
              key={row.id}
              row={row}
              projectKey={projectKey}
              handle={handles}
              flash={flashing.has(row.id)}
              onOver={reparent.over}
              onDrop={(target) => reparent.drop(row.id, target)}
            />
          ) : (
            <TouchTreeGroup
              key={row.id}
              row={row}
              path={treePath(rows, row)}
              open={!collapsed.has(row.id)}
              over={reparent.overId === row.id}
              rollup={rollupText(row, includeCompleted)}
              onToggle={() => toggle(row.id)}
            />
          ),
        )}
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
