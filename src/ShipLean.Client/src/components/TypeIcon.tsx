import { componentTestIds } from './testIds';

import type { WorkItemTypeId } from '../api/refs';

export type WorkItemType = WorkItemTypeId;

const letters: Record<WorkItemType, string> = {
  epic: 'E',
  feature: 'F',
  story: 'S',
  bug: 'B',
  task: 'T',
  action: 'A',
};

/** The 16px coloured square with a letter that marks a work item's type. */
export function TypeIcon({ type }: { type: WorkItemType }) {
  return (
    <span
      data-testid={componentTestIds.typeIcon}
      data-type={type}
      title={type}
      className="inline-grid size-4 flex-none place-items-center rounded-[var(--radius-xs)] text-[9px] leading-none font-bold text-signal-ink"
      style={{ background: `var(--type-${type})` }}
    >
      {letters[type]}
    </span>
  );
}
