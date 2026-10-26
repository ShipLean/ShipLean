import { useState } from 'react';
import { settingsTestIds } from '../testIds';
import { itemsIn, type StateRowModel } from './stateModel';

/** Asks where a deleted state's items go. The move happens on Save. */
export function DeleteStateForm({
  row,
  targets,
  initial,
  onConfirm,
  onCancel,
}: {
  row: StateRowModel;
  targets: StateRowModel[];
  /** The state the items go to unless the user picks another. */
  initial: string;
  onConfirm(targetId: string): void;
  onCancel(): void;
}) {
  const [target, setTarget] = useState(initial);
  const items = itemsIn(row);
  return (
    <div
      data-testid={settingsTestIds.stateDeleteForm}
      className="flex flex-wrap items-center gap-2 border-t border-edge bg-panel-sunken px-3 py-2 text-[length:var(--text-sm)]"
    >
      <span className="text-ink">
        Delete <b>{row.name}</b>? {items} work {items === 1 ? 'item moves' : 'items move'} to:
      </span>
      <select
        data-testid={settingsTestIds.stateDeleteTarget}
        aria-label={`Move the items of ${row.name} to`}
        className="h-7 rounded-control border border-edge bg-panel px-2 text-ink"
        value={target}
        onChange={(e) => setTarget(e.target.value)}
      >
        <option value="">Choose a state</option>
        {targets.map((t) => (
          <option key={t.id} value={t.id}>
            {t.name}
          </option>
        ))}
      </select>
      <button
        type="button"
        data-testid={settingsTestIds.stateDeleteConfirm}
        disabled={target === ''}
        className="h-7 rounded-control border border-alert px-2 text-alert disabled:opacity-60"
        onClick={() => onConfirm(target)}
      >
        Delete state
      </button>
      <button
        type="button"
        data-testid={settingsTestIds.stateDeleteCancel}
        className="h-7 px-2 text-ink-muted"
        onClick={onCancel}
      >
        Cancel
      </button>
    </div>
  );
}
