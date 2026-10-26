import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { registerInvalidation } from '../../../realtime';
import { workItemAttachmentsKey } from '../workItemApi';

// A teammate added or archived an attachment on this item: read the list again. Own writes invalidate directly
// (useAttachmentWrites), and the event of this tab's own write never arrives here.
export function useAttachmentEvents(
  projectId: string,
  itemKey: string,
  workItemId: string | undefined,
) {
  const queryClient = useQueryClient();
  useEffect(() => {
    if (workItemId === undefined) return undefined;
    return registerInvalidation('attachmentsChanged', (event) => {
      if (event.workItemId === workItemId) {
        void queryClient.invalidateQueries({
          queryKey: workItemAttachmentsKey(projectId, itemKey),
        });
      }
    });
  }, [projectId, itemKey, workItemId, queryClient]);
}
