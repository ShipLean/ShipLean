import { boardSettingsTestIds as ids } from '../../testIds';

/** The delete guard: a column that holds cards can't go until they are moved; says how many. */
export function BlockedDelete({
  name,
  count,
  onDismiss,
}: {
  name: string;
  count: number;
  onDismiss(): void;
}) {
  return (
    <div
      role="alert"
      data-testid={ids.columnBlocked}
      className="flex items-start gap-3 rounded-panel border border-caution bg-panel p-3 text-[length:var(--text-sm)]"
    >
      <div className="grid flex-1 gap-0.5">
        <b>
          {name} has {count} {count === 1 ? 'card' : 'cards'}, so it can't be deleted yet.
        </b>
        <span className="text-ink-muted">
          Move them to another column on the board first, then delete it. Deleting a column never
          changes a work item's state.
        </span>
      </div>
      <button
        type="button"
        data-testid={ids.columnBlockedDismiss}
        aria-label="Dismiss"
        className="inline-grid size-6 place-items-center rounded-control text-ink-muted hover:bg-sunken"
        onClick={onDismiss}
      >
        ✕
      </button>
    </div>
  );
}
