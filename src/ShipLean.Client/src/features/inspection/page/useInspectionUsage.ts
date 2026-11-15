import { useCallback } from 'react';
import { postInspectionUsage, type InspectionUsageAction } from '@shiplean/core/api/inspection';

/** Sends one viewing event for the item; a failed send is never the viewer's problem. */
export function useInspectionUsage(projectId: string, itemKey: string) {
  return useCallback(
    (action: InspectionUsageAction) => {
      postInspectionUsage(projectId, { action, itemKey }).catch(() => undefined);
    },
    [projectId, itemKey],
  );
}
