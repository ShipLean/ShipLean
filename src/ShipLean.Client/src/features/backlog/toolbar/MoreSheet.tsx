import type { MouseEvent, ReactNode } from 'react';
import { actionSheetRowClass, ActionSheetRow, type SheetAction } from '../../../touch/ActionSheet';
import { MoreButton } from '../../../touch/MoreButton';
import { backlogTestIds as ids } from '../testIds';

export interface MoreSheetProps {
  // Search this list is offered when given; it opens the field under the toolbar.
  onSearch?: () => void;
  // The mode's own controls (Add many, Forecast), rendered as given.
  options?: () => ReactNode;
  // Rows that follow everything else.
  moreActions?: readonly SheetAction[];
  // The column sort in force, with the one action that clears it.
  sort?: { label: string; onClear: () => void };
  includeCompleted?: boolean;
  onIncludeCompletedChange?: (includeCompleted: boolean) => void;
}

// The touch toolbar's ⋯ More (SHIP-716): the K5 MoreButton, with Search this list, the mode's options, the sort and Show
// completed in its children slot and the mode's actions as its action rows. Nothing to offer, no button.
export function MoreSheet({
  onSearch,
  options,
  moreActions,
  sort,
  includeCompleted,
  onIncludeCompletedChange,
}: MoreSheetProps) {
  const completed = includeCompleted !== undefined && onIncludeCompletedChange !== undefined;
  if (!onSearch && !options && !moreActions?.length && !sort && !completed) return null;

  return (
    <MoreButton actions={moreActions ?? []}>
      {(close) => (
        <>
          {onSearch ? (
            <ActionSheetRow
              action={{ id: 'search', label: 'Search this list', onSelect: onSearch }}
              onClose={close}
            />
          ) : null}
          {options ? (
            // A button the mode rendered (Add many) has done its job once pressed; a checkbox (Forecast) keeps the sheet open.
            <div
              data-testid={ids.moreOptions}
              className="grid gap-1"
              onClick={(event: MouseEvent) => {
                if ((event.target as HTMLElement).closest('button')) close();
              }}
            >
              {options()}
            </div>
          ) : null}
          {sort ? (
            <div data-testid={ids.moreSort} className={`${actionSheetRowClass} gap-2 text-ink`}>
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
            <label className={`${actionSheetRowClass} gap-3 text-ink`}>
              <input
                type="checkbox"
                data-testid={ids.completedToggle}
                checked={includeCompleted}
                onChange={(event) => onIncludeCompletedChange(event.target.checked)}
              />
              Show completed
            </label>
          ) : null}
        </>
      )}
    </MoreButton>
  );
}
