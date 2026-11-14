import { taskChangesTestIds as ids } from './testIds';
import type { ChangesData, ReviewActions } from './types';

export interface ReviewBarProps {
  data: ChangesData;
  projectKey: string;
  itemKey: string;
  /** ChangesScreen adds the head commit id it showed to Approve (D21). */
  review: ReviewActions;
}

// Stub: the Approve and Request changes buttons and their dialogs replace this.
export function ReviewBar({ data }: ReviewBarProps) {
  return <div data-testid={ids.reviewBar}>{data.openComments} open</div>;
}
