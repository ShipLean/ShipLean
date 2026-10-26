import { api } from '../../../api/client';
import type { WorkItemDto } from '../../../api/workItems';

/** Still true: stamps the status as verified now, in one revision that changes nothing else. Answers the item as it now is. */
export const confirmStatus = (workItemId: string, revision: number) =>
  api.post<WorkItemDto>(`/api/work-items/${encodeURIComponent(workItemId)}/status/confirm`, {
    revision,
  });
