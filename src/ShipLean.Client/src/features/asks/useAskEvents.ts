import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { registerInvalidation } from '../../realtime';
import { workItemHistoryKey } from '../workItem/workItemApi';
import { asksKey } from './asksApi';

// A teammate or an agent changed this item's asks: read the asks and History again. Own writes invalidate directly
// (useAskWrites), and the event of this tab's own write never arrives here.
export function useAskEvents(projectId: string, itemKey: string, workItemId: string) {
  const queryClient = useQueryClient();
  useEffect(
    () =>
      registerInvalidation('asksChanged', (event) => {
        if (event.workItemId !== workItemId) return;
        void queryClient.invalidateQueries({ queryKey: asksKey(projectId, itemKey) });
        void queryClient.invalidateQueries({ queryKey: workItemHistoryKey(projectId, itemKey) });
      }),
    [projectId, itemKey, workItemId, queryClient],
  );
}
