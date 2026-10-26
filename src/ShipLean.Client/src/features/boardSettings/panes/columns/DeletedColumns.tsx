import type { BoardColumnJson } from '../../../../api/persisted';
import { boardSettingsTestIds as ids } from '../../testIds';

export interface DeletedColumn {
  column: BoardColumnJson;
  /** Where it sat, so Undo puts it back. */
  index: number;
}

/** Columns removed in this session of the pane: they go at Save, and Undo brings one back (a refused Save leaves it here). */
export function DeletedColumns({
  deleted,
  readOnly,
  onUndo,
}: {
  deleted: DeletedColumn[];
  readOnly: boolean;
  onUndo(entry: DeletedColumn): void;
}) {
  return (
    <>
      {deleted.map((entry) => (
        <div
          key={entry.column.id}
          data-testid={ids.columnDeleted}
          className="flex items-center justify-between gap-3 rounded-panel border border-edge border-l-[3px] border-l-caution bg-panel px-3 py-2 text-[length:var(--text-sm)]"
        >
          <span>
            Deleted on Save: <b>{entry.column.name}</b>
          </span>
          <button
            type="button"
            data-testid={ids.columnUndoDelete}
            disabled={readOnly}
            className="h-7 flex-none rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] disabled:opacity-50"
            onClick={() => onUndo(entry)}
          >
            Undo
          </button>
        </div>
      ))}
    </>
  );
}
