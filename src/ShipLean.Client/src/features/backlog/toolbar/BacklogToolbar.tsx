import type { ReactNode } from 'react';
import { backlogModes, type BacklogMode, type Level } from '../../../routes';
import { backlogTestIds as ids } from '../testIds';
import { ColumnsMenu, type ColumnsMenuProps } from './ColumnsMenu';
import { ModeSwitch } from './ModeSwitch';

// FROZEN: the filter, every backlog level and every mode render this one toolbar. Add optional props, never rename or
// remove one. A control whose props are absent is hidden.
export interface BacklogToolbarProps {
  projectKey: string;
  level: Level;
  // The mode the open page is in.
  mode: BacklogMode;
  // The modes the switch offers, in order; none hides the switch (Features and Epics have no modes).
  modes?: readonly BacklogMode[];
  onNewItem: () => void;
  // The New button's label; defaults to "+ New work item".
  newItemLabel?: string;
  // The Completed toggle shows only when both are given.
  includeCompleted?: boolean;
  onIncludeCompletedChange?: (includeCompleted: boolean) => void;
  // The Columns menu shows only when given.
  columns?: ColumnsMenuProps;
  // Renders extra controls (a Forecast toggle, Show parents) on the right, before Completed.
  options?: () => ReactNode;
  // Renders the filter (the criteria builder and its chips) beside the buttons. Absent, nothing shows.
  filter?: () => ReactNode;
}

export function BacklogToolbar({
  projectKey,
  level,
  mode,
  modes = backlogModes,
  onNewItem,
  newItemLabel = '+ New work item',
  includeCompleted,
  onIncludeCompletedChange,
  columns,
  options,
  filter,
}: BacklogToolbarProps) {
  return (
    <div
      data-testid={ids.toolbar}
      className="flex flex-none flex-wrap items-center gap-[var(--space-2)] border-b border-edge bg-panel px-[var(--space-4)] py-[var(--space-2)]"
    >
      {modes.length > 0 ? (
        <ModeSwitch projectKey={projectKey} level={level} mode={mode} modes={modes} />
      ) : null}
      <button type="button" data-testid={ids.newItem} className="btn-signal" onClick={onNewItem}>
        {newItemLabel}
      </button>
      <div data-testid={ids.filterSlot} className="flex items-center gap-[var(--space-2)]">
        {filter?.()}
      </div>
      <span className="flex-1" />
      {options?.()}
      {includeCompleted !== undefined && onIncludeCompletedChange ? (
        <label className="inline-flex items-center gap-1.5 text-[length:var(--text-sm)] text-ink">
          <input
            type="checkbox"
            data-testid={ids.completedToggle}
            checked={includeCompleted}
            onChange={(event) => onIncludeCompletedChange(event.target.checked)}
          />
          Completed
        </label>
      ) : null}
      {columns ? <ColumnsMenu {...columns} /> : null}
    </div>
  );
}
