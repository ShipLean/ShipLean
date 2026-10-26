import { sprintsTestIds as ids } from '../testIds';

interface CapacityToolbarProps {
  /** The sprint before this one; none for the first. */
  previousName: string | undefined;
  /** Why Copy is off (nothing to copy, or it could not be loaded); undefined when it can run. */
  copyBlocked: string | undefined;
  dirty: boolean;
  saving: boolean;
  onCopy: () => void;
  onClear: () => void;
  onSave: () => void;
}

const button =
  'inline-flex h-7 items-center rounded-control border border-edge bg-panel px-2.5 text-[length:var(--text-sm)] font-medium text-ink hover:bg-signal-soft disabled:opacity-50';

/** Fill the rows from the sprint before, empty them, or save what is on screen. */
export function CapacityToolbar({
  previousName,
  copyBlocked,
  dirty,
  saving,
  onCopy,
  onClear,
  onSave,
}: CapacityToolbarProps) {
  return (
    <div className="flex flex-wrap items-center justify-end gap-2 p-4 pb-0">
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
        title="Remove all entries; plan against velocity"
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
