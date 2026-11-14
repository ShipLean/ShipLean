import { taskChangesTestIds as ids } from './testIds';
import type { ChangesData } from './types';

export interface CommentsColumnProps {
  data: ChangesData;
  onReply(commentId: string, body: string): Promise<void>;
  onResolve(commentId: string, resolved: boolean): Promise<void>;
}

// Stub: the threads with reply and resolve replace this.
export function CommentsColumn({ data }: CommentsColumnProps) {
  return <div data-testid={ids.comments}>{data.openComments} open comments</div>;
}
