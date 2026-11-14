import { useState, type MouseEvent, type ReactNode } from 'react';
import { Sheet } from '../../../touch/Sheet';
import type { SheetAction } from '../../../touch/ActionSheet';
import { touchTestIds } from '../../../touch/testIds';
import { backlogTestIds as ids } from '../testIds';

const rowClass =
  'flex min-h-[var(--touch-target)] w-full items-center rounded-control px-2 py-1.5 text-left text-[length:var(--text-base)] active:bg-signal-soft disabled:cursor-default disabled:text-ink-muted disabled:active:bg-transparent';

export interface MoreSheetProps {
  // Search this list is offered when given; it opens the field under the toolbar.
  onSearch?: () => void;
  // The mode's own controls (Add many, Forecast), rendered as given.
  options?: () => ReactNode;
  // Rows that follow the options.
  moreActions?: readonly SheetAction[];
  // The column sort in force, with the one action that clears it.
  sort?: { label: string; onClear: () => void };
  includeCompleted?: boolean;
  onIncludeCompletedChange?: (includeCompleted: boolean) => void;
}

// The touch toolbar's ⋯ More (SHIP-716): a bottom sheet of Search this list, the mode's options and actions, the sort
// and Show completed. Built on the K5 Sheet because the options are controls the mode renders, which an ActionSheet's
// plain action rows can't hold. Nothing to offer, no button.
export function MoreSheet({
  onSearch,
  options,
  moreActions,
  sort,
  includeCompleted,
  onIncludeCompletedChange,
}: MoreSheetProps) {
  const [open, setOpen] = useState(false);
  const close = () => setOpen(false);
  const completed = includeCompleted !== undefined && onIncludeCompletedChange !== undefined;
  if (!onSearch && !options && !moreActions?.length && !sort && !completed) return null;

  // A button the mode rendered (Add many) has done its job once pressed; a checkbox (Forecast) keeps the sheet open.
  const closeAfterButton = (event: MouseEvent) => {
    if ((event.target as HTMLElement).closest('button')) close();
  };
  return (
    <>
      <button
        type="button"
        data-testid={touchTestIds.moreButton}
        aria-label="More"
        aria-haspopup="dialog"
        aria-expanded={open}
        className="inline-grid size-[var(--touch-target)] flex-none place-items-center rounded-control text-[length:var(--text-md)] leading-none text-ink-muted hover:text-ink active:bg-signal-soft"
        onClick={() => setOpen(true)}
      >
        ⋯
      </button>
      <Sheet open={open} title="More" onClose={close}>
        {onSearch ? (
          <button
            type="button"
            data-testid={ids.moreSearch}
            className={`${rowClass} text-ink`}
            onClick={() => {
              close();
              onSearch();
            }}
          >
            Search this list
          </button>
        ) : null}
        {options ? (
          <div data-testid={ids.moreOptions} className="grid gap-1" onClick={closeAfterButton}>
            {options()}
          </div>
        ) : null}
        {moreActions?.map((action) => (
          <button
            key={action.id}
            type="button"
            data-testid={touchTestIds.actionSheetRow}
            data-action-id={action.id}
            disabled={action.disabled}
            className={`${rowClass} ${action.tone === 'danger' ? 'text-alert' : 'text-ink'}`}
            onClick={() => {
              action.onSelect();
              close();
            }}
          >
            {action.label}
          </button>
        ))}
        {sort ? (
          <div data-testid={ids.moreSort} className={`${rowClass} gap-2 text-ink`}>
            <span className="min-w-0 flex-1">Sorted by {sort.label}</span>
            <button
              type="button"
              data-testid={ids.moreSortClear}
              className="btn-ghost flex-none touch:min-h-[var(--touch-target)]"
              onClick={() => {
                sort.onClear();
                close();
              }}
            >
              Clear sort
            </button>
          </div>
        ) : null}
        {completed ? (
          <label className={`${rowClass} gap-3 text-ink`}>
            <input
              type="checkbox"
              data-testid={ids.completedToggle}
              checked={includeCompleted}
              onChange={(event) => onIncludeCompletedChange(event.target.checked)}
            />
            Show completed
          </label>
        ) : null}
      </Sheet>
    </>
  );
}
