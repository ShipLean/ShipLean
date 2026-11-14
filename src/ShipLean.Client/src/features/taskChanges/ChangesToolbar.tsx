import { taskChangesTestIds as ids } from './testIds';
import type { ChangesData, DiffLayout } from './types';

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
}

// Stub: the commit picker, layout toggle, totals, viewed progress and new-commit banner replace this. Approve and Request
// changes are ReviewBar's, which ChangesScreen renders at the end of the same row.
export function ChangesToolbar({ data, newCommit }: ChangesToolbarProps) {
  return (
    <div data-testid={ids.toolbar}>
      {data.commits.length} commits{newCommit ? ' (new commit)' : ''}
    </div>
  );
}
