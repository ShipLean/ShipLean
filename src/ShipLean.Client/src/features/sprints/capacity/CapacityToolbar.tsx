import type { UserRef } from '../../../api/refs';
import { sprintsTestIds as ids } from '../testIds';

interface CapacityToolbarProps {
  /** Project members who are not in the table yet. */
  addable: UserRef[];
  /** The sprint before this one; none for the first. */
  previousName: string | undefined;
  /** Why Copy is off (nothing to copy, or it could not be loaded); undefined when it can run. */
  copyBlocked: string | undefined;
  dirty: boolean;
  saving: boolean;
  onAdd: (userId: string) => void;
  onCopy: () => void;
  onClear: () => void;
  onSave: () => void;
}

const button =
  'inline-flex h-7 items-center rounded-control border border-edge bg-panel px-2.5 text-[length:var(--text-sm)] font-medium text-ink hover:bg-signal-soft disabled:opacity-50';

/** Add a member, fill the rows from the sprint before, empty them, or save what is on screen. */
export function CapacityToolbar({
  addable,
  previousName,
  copyBlocked,
  dirty,
  saving,
  onAdd,
  onCopy,
  onClear,
  onSave,
}: CapacityToolbarProps) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <select
        data-testid={ids.capacityAdd}
        aria-label="Add member"
        className="h-7 rounded-control border border-edge bg-panel px-1 text-[length:var(--text-sm)] text-ink disabled:opacity-50"
        value=""
        disabled={addable.length === 0}
        title={addable.length === 0 ? 'Every project member is already in the table' : undefined}
        onChange={(event) => {
          if (event.target.value !== '') onAdd(event.target.value);
        }}
      >
        <option value="">+ Add member</option>
        {addable.map((user) => (
          <option key={user.id} value={user.id}>
            {user.name}
          </option>
        ))}
      </select>
      <span className="grow" />
      <button
        type="button"
        data-testid={ids.capacityCopy}
        className={button}
        disabled={copyBlocked !== undefined}
        title={copyBlocked}
        onClick={onCopy}
      >
        {previousName === undefined ? 'Copy from previous sprint' : `Copy from ${previousName}`}
      </button>
      <button
        type="button"
        data-testid={ids.capacityClear}
        className={button}
        title="Remove everyone from the table; plan against velocity"
        onClick={onClear}
      >
        Clear
      </button>
      <button
        type="button"
        data-testid={ids.capacitySave}
        className="inline-flex h-7 items-center rounded-control border border-signal bg-signal px-2.5 text-[length:var(--text-sm)] font-medium text-signal-ink disabled:opacity-50"
        disabled={!dirty || saving}
        onClick={onSave}
      >
        Save
      </button>
    </div>
  );
}
