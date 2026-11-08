import { Sheet } from './Sheet';
import { touchTestIds } from './testIds';

/** One row of an `ActionSheet`. */
export type SheetAction = {
  id: string;
  label: string;
  onSelect: () => void;
  tone?: 'danger';
  disabled?: boolean;
};

/** A `Sheet` of action rows (K5). Picking a row runs it, then closes the sheet. */
export function ActionSheet({
  open,
  title,
  actions,
  onClose,
}: {
  open: boolean;
  title?: string;
  actions: readonly SheetAction[];
  onClose: () => void;
}) {
  return (
    <Sheet open={open} title={title ?? ''} onClose={onClose}>
      {actions.map((action) => (
        <button
          key={action.id}
          type="button"
          data-testid={touchTestIds.actionSheetRow}
          data-action-id={action.id}
          disabled={action.disabled}
          className={`flex min-h-[var(--touch-target)] w-full items-center rounded-control px-2 py-1.5 text-left text-[length:var(--text-base)] active:bg-signal-soft disabled:cursor-default disabled:text-ink-muted disabled:active:bg-transparent ${action.tone === 'danger' ? 'text-alert' : 'text-ink'}`}
          onClick={() => {
            action.onSelect();
            onClose();
          }}
        >
          {action.label}
        </button>
      ))}
    </Sheet>
  );
}
