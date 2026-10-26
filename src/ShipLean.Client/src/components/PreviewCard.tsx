import type { ReactNode } from 'react';
import { Avatar, type AvatarProps } from './Avatar';
import { StateDot, type StateCategory } from './StateDot';
import { componentTestIds } from './testIds';
import { TypeIcon, type WorkItemType } from './TypeIcon';

/** A compact work item preview for hovers and link previews. `children` is the body slot under the title row. */
export function PreviewCard({
  type,
  itemKey,
  title,
  stateName,
  stateCategory,
  assignee,
  children,
}: {
  type: WorkItemType;
  itemKey: string;
  title: string;
  stateName: string;
  stateCategory: StateCategory;
  /** Null for an unassigned item (a dashed avatar). */
  assignee: AvatarProps | null;
  children?: ReactNode;
}) {
  return (
    <div
      data-testid={componentTestIds.previewCard}
      className="grid w-72 gap-2 rounded-panel border border-edge bg-panel p-3 shadow-panel"
    >
      <div className="flex items-center gap-2 text-[length:var(--text-xs)] text-ink-muted">
        <TypeIcon type={type} />
        <span data-testid={componentTestIds.previewCardKey} className="font-medium">
          {itemKey}
        </span>
        <span className="ml-auto" data-testid={componentTestIds.previewCardState}>
          <StateDot category={stateCategory} name={stateName} />
        </span>
        <Avatar {...(assignee ?? { initials: null })} />
      </div>
      <div
        data-testid={componentTestIds.previewCardTitle}
        className="text-[length:var(--text-base)] font-medium text-ink"
      >
        {title}
      </div>
      {children ? (
        <div
          data-testid={componentTestIds.previewCardBody}
          className="text-[length:var(--text-sm)] text-ink-muted"
        >
          {children}
        </div>
      ) : null}
    </div>
  );
}
