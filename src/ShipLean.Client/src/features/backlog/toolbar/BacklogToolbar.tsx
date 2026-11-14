import { useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import type { SheetAction } from '../../../touch/ActionSheet';
import { useTouchShell } from '../../../touch/useTouchShell';
import { TouchSearchClose } from '../search/touchSearch';
import { backlogModes, type BacklogMode, type Level } from '../../../routes';
import { backlogTestIds as ids } from '../testIds';
import { ColumnsMenu, type ColumnsMenuProps } from './ColumnsMenu';
import { ModeSwitch } from './ModeSwitch';
import { MoreSheet } from './MoreSheet';
import { ToolbarChipsHost } from './touchSlots';
import { TriageChip } from './TriageChip';

// FROZEN: the filter, every backlog level and every mode render this one toolbar. Add optional props, never rename or
// remove one. A control whose props are absent is hidden.
export interface BacklogToolbarProps {
  projectKey: string;
  level: Level;
  // The mode the open page is in.
  mode: BacklogMode;
  // The modes the switch offers, in order; none hides the switch (Epics have no modes, Features List and Tree).
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
  // Renders the search field beside the filter. Absent, nothing shows.
  search?: () => ReactNode;
  // Renders the keyboard shortcuts button at the right end. Absent, nothing shows.
  keys?: () => ReactNode;
  // The items waiting in Triage and what the chip does when clicked (filters the backlog to them). No chip while the count is 0 or absent.
  triage?: { count: number; onShow: () => void };
  // At 900px and below: rows that follow the options in ⋯ More (Select, Estimate; Planning's Override velocity, Create sprint).
  moreActions?: readonly SheetAction[];
  // At 900px and below: the column sort in force, shown in ⋯ More with the action that clears it. Absent, no sort row.
  sort?: { label: string; onClear: () => void };
}

export function BacklogToolbar({
  projectKey,
  level,
  mode,
  modes = backlogModes,
  onNewItem,
  newItemLabel,
  includeCompleted,
  onIncludeCompletedChange,
  columns,
  options,
  filter,
  search,
  keys,
  triage,
  moreActions,
  sort,
}: BacklogToolbarProps) {
  const { touch } = useTouchShell();
  const [chipsHost, setChipsHost] = useState<HTMLElement | null>(null);
  const [searching, setSearching] = useState(false);
  if (touch) {
    // One row: + New, the mode switch, Filter and ⋯ More. Everything else is in More; the filter's chips and the Triage chip
    // sit under the row, and so does the search field once More opens it. Columns and Keys aren't offered (D7).
    return (
      <div data-testid={ids.toolbar} className="flex-none border-b border-edge bg-panel">
        <div
          data-testid={ids.toolbarRow}
          className="flex flex-nowrap items-center gap-[var(--space-2)] px-3 py-[var(--space-2)]"
        >
          <button
            type="button"
            data-testid={ids.newItem}
            className="btn-signal flex-none"
            onClick={onNewItem}
          >
            {newItemLabel ?? '+ New'}
          </button>
          {modes.length > 0 ? (
            <ModeSwitch projectKey={projectKey} level={level} mode={mode} modes={modes} />
          ) : null}
          <ToolbarChipsHost.Provider value={{ host: chipsHost }}>
            <div data-testid={ids.filterSlot} className="flex flex-none items-center">
              {filter?.()}
            </div>
          </ToolbarChipsHost.Provider>
          <span className="flex-1" />
          <MoreSheet
            onSearch={search ? () => setSearching(true) : undefined}
            options={options}
            moreActions={moreActions}
            sort={sort}
            includeCompleted={includeCompleted}
            onIncludeCompletedChange={onIncludeCompletedChange}
          />
        </div>
        <div
          ref={setChipsHost}
          data-testid={ids.filterChips}
          className="flex flex-wrap items-center gap-[var(--space-2)] px-3 pb-[var(--space-2)] empty:hidden"
        />
        {triage && chipsHost
          ? createPortal(<TriageChip count={triage.count} onShow={triage.onShow} />, chipsHost)
          : null}
        {search && searching ? (
          <div
            data-testid={ids.searchRow}
            className="flex items-center gap-[var(--space-2)] px-3 pb-[var(--space-2)]"
          >
            <TouchSearchClose.Provider value={() => setSearching(false)}>
              {search()}
            </TouchSearchClose.Provider>
          </div>
        ) : null}
      </div>
    );
  }
  return (
    <div
      data-testid={ids.toolbar}
      className="flex flex-none flex-wrap items-center gap-[var(--space-2)] border-b border-edge bg-panel px-[var(--space-4)] py-[var(--space-2)]"
    >
      {modes.length > 0 ? (
        <ModeSwitch projectKey={projectKey} level={level} mode={mode} modes={modes} />
      ) : null}
      <button type="button" data-testid={ids.newItem} className="btn-signal" onClick={onNewItem}>
        {newItemLabel ?? '+ New work item'}
      </button>
      <div data-testid={ids.filterSlot} className="flex items-center gap-[var(--space-2)]">
        {filter?.()}
      </div>
      {search?.()}
      {triage ? <TriageChip count={triage.count} onShow={triage.onShow} /> : null}
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
      {keys?.()}
    </div>
  );
}
