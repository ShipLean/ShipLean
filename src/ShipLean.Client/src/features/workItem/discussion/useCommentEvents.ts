import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { registerInvalidation } from '../../../realtime';
import { workItemCommentsKey } from '../workItemApi';

// A teammate added, edited or archived a comment on this item: read the discussion again. Own writes invalidate directly
// (useCommentWrites), and the event of this tab's own write never arrives here.
export function useCommentEvents(
  projectId: string,
  itemKey: string,
  workItemId: string | undefined,
) {
  const queryClient = useQueryClient();
  useEffect(() => {
    if (workItemId === undefined) return undefined;
    return registerInvalidation('commentsChanged', (event) => {
      if (event.workItemId === workItemId) {
        void queryClient.invalidateQueries({ queryKey: workItemCommentsKey(projectId, itemKey) });
      }
    });
  }, [projectId, itemKey, workItemId, queryClient]);
}
