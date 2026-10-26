import { useEffect, useRef, useState } from 'react';
import type { BoardColumnJson, BoardSettingsJson } from '../../../api/persisted';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsContext } from '../panes';
import { boardSettingsTestIds } from '../testIds';
import {
  columnForState,
  deadMappings,
  insertBeforeLast,
  replaceColumn,
  setState,
  unmappedStates,
  type Fix,
} from './columns/columnEdits';
import { BlockedDelete } from './columns/BlockedDelete';
import { useColumnCounts } from './columns/useColumnCounts';
import { DeletedColumns, type DeletedColumn } from './columns/DeletedColumns';
import { ColumnsTable } from './columns/ColumnsTable';
import { UnmappedStates } from './columns/UnmappedStates';

/** The nav mark and footer chip of the Columns pane: states of the shown types that no column of the draft maps. */
export function columnsWarning(
  draft: BoardSettingsJson,
  context: BoardSettingsContext,
): string | null {
  // The Taskboard's columns are the Task states themselves, so none can be unmapped.
  if (context.level === 'taskboard') return null;
  const unmapped = unmappedStates(draft.columns, context.states).length;
  const dead = deadMappings(draft.columns, context.states).length;
  const parts = [
    unmapped === 0 ? null : `${unmapped} ${unmapped === 1 ? 'state' : 'states'} not on any column`,
    dead === 0 ? null : `${dead} ${dead === 1 ? 'mapping' : 'mappings'} to a removed state`,
  ].filter((part) => part !== null);
  return parts.length === 0 ? null : parts.join(' · ');
}

/** Columns are not states: each column means one state per work item type of the board. */
export function ColumnsPane({
  draft,
  onChange,
  readOnly,
  context,
}: PaneProps<BoardSettingsJson, BoardSettingsContext>) {
  const unmapped = unmappedStates(draft.columns, context.states);
  const [deleted, setDeleted] = useState<DeletedColumn[]>([]);
  const [blocked, setBlocked] = useState<{ id: string; count: number }>();
  const countNow = useColumnCounts(
    context.projectId,
    context.boardId,
    context.level !== 'taskboard',
  );
  // The latest draft: a delete that waited for the board read applies to what the pane holds now, not to the click.
  const latest = useRef(draft);
  useEffect(() => {
    latest.current = draft;
  }, [draft]);
  const setColumns = (columns: BoardColumnJson[]) => {
    latest.current = { ...latest.current, columns };
    onChange(latest.current);
  };
  const remove = async (column: BoardColumnJson) => {
    // A board that can't say how many cards a column holds doesn't block: the server refuses the save of one that has some.
    const count = await countNow(column.id);
    if (count !== undefined && count > 0) {
      setBlocked({ id: column.id, count });
      return;
    }
    const index = latest.current.columns.findIndex((c) => c.id === column.id);
    if (index < 0) return;
    setDeleted((entries) => [...entries, { column, index }]);
    setColumns(latest.current.columns.filter((c) => c.id !== column.id));
  };
  const undo = (entry: DeletedColumn) => {
    setDeleted(deleted.filter((d) => d !== entry));
    const next = [...draft.columns];
    next.splice(Math.min(entry.index, next.length - 1), 0, entry.column);
    setColumns(next);
  };
  const blockedColumn = draft.columns.find((c) => c.id === blocked?.id);
  const fix = (applied: Fix) =>
    setColumns(
      applied.kind === 'remap'
        ? replaceColumn(
            draft.columns,
            applied.column.id,
            setState(applied.column, applied.unmapped.type, applied.unmapped.state.id),
          )
        : insertBeforeLast(draft.columns, columnForState(context.states, applied.unmapped)),
    );
  return (
    <div data-testid={boardSettingsTestIds.columnsPane} className="grid content-start gap-3">
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Columns are not states. Each column maps to one state for each work item type on this board:
        dragging a card into a column sets that state, and a state changed elsewhere moves the card
        to the first column mapped to it.
      </p>
      <UnmappedStates
        columns={draft.columns}
        states={context.states}
        unmapped={unmapped}
        readOnly={readOnly}
        onFix={fix}
      />
      <ColumnsTable
        columns={draft.columns}
        states={context.states}
        readOnly={readOnly}
        onChange={setColumns}
        onDelete={remove}
      />
      {blocked !== undefined && blockedColumn !== undefined ? (
        <BlockedDelete
          name={blockedColumn.name}
          count={blocked.count}
          onDismiss={() => setBlocked(undefined)}
        />
      ) : null}
      <DeletedColumns
        deleted={deleted.filter((d) => !draft.columns.some((c) => c.id === d.column.id))}
        readOnly={readOnly}
        onUndo={undo}
      />
    </div>
  );
}
