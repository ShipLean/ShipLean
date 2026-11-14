import { taskChangesTestIds as ids } from './testIds';
import type { ChangesData, DiffLayout, ReviewActions } from './types';

export interface ChangesToolbarProps {
  data: ChangesData;
  layout: DiffLayout;
  /** "n of m files viewed" at the current head. */
  viewedCount: number;
  fileTotal: number;
  /** A teammate added a commit since this page loaded; the banner offers `onReload`. */
  newCommit: boolean;
  onReload(): void;
  /** "all" or a work_item_commit id. */
  onPickCommit(commit: string): void;
  onLayout(layout: DiffLayout): void;
  review: ReviewActions;
}

// Stub: the commit picker, layout toggle, new-commit banner and Approve / Request changes dialogs replace this.
export function ChangesToolbar({ data, newCommit }: ChangesToolbarProps) {
  return (
    <div data-testid={ids.toolbar}>
      {data.commits.length} commits{newCommit ? ' (new commit)' : ''}
    </div>
  );
}
