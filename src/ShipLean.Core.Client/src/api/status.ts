// A Feature's status line: confirm it is still true, or write a new one. C#: StatusConfirmRequest and the work item PATCH.
// The SPA keeps its own copy in its status feature.
import { api } from './client';
import type { WorkItemDto, WriteResult } from './workItems';

/** 200 with the item as it is now; a stale revision is 409 with a ConflictDto (read it with conflictOf). */
export const confirmStatus = (itemId: string, revision: number) =>
  api.post<WorkItemDto>(`/api/work-items/${encodeURIComponent(itemId)}/status/confirm`, {
    revision,
  });

/**
 * Writes the status line, which also stamps it as verified now. Only statusLine is sent: the server leaves every field
 * the patch doesn't name alone. A stale expectedRevision is 409 with a ConflictDto.
 */
export const setStatusLine = (itemId: string, expectedRevision: number, text: string) =>
  api.patch<WriteResult>(`/api/work-items/${encodeURIComponent(itemId)}`, {
    expectedRevision,
    changes: { statusLine: { value: text } },
  });
