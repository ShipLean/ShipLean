import type { ReactNode } from 'react';
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

/** What a sheet's own rows look like; a row that is not an action (a checkbox, a status line) takes it too. */
export const actionSheetRowClass =
  'flex min-h-[var(--touch-target)] w-full items-center rounded-control px-2 py-1.5 text-left text-[length:var(--text-base)] active:bg-signal-soft disabled:cursor-default disabled:text-ink-muted disabled:active:bg-transparent';

/** The content a sheet shows before its action rows: nodes, or a function given the sheet's close. */
export type SheetSlot = ReactNode | ((close: () => void) => ReactNode);

/** One action row: running it closes the sheet. */
export function ActionSheetRow({ action, onClose }: { action: SheetAction; onClose: () => void }) {
  return (
    <button
      type="button"
      data-testid={touchTestIds.actionSheetRow}
      data-action-id={action.id}
      disabled={action.disabled}
      className={`${actionSheetRowClass} ${action.tone === 'danger' ? 'text-alert' : 'text-ink'}`}
      onClick={() => {
        action.onSelect();
        onClose();
      }}
    >
      {action.label}
    </button>
  );
}

/** A `Sheet` of action rows (K5), with an optional `children` slot before them. Picking a row runs it, then closes the sheet. */
export function ActionSheet({
  open,
  title,
  actions,
  onClose,
  children,
}: {
  open: boolean;
  title?: string;
  actions: readonly SheetAction[];
  onClose: () => void;
  children?: SheetSlot;
}) {
  return (
    <Sheet open={open} title={title ?? ''} onClose={onClose}>
      {typeof children === 'function' ? children(onClose) : children}
      {actions.map((action) => (
        <ActionSheetRow key={action.id} action={action} onClose={onClose} />
      ))}
    </Sheet>
  );
}
