import { api } from '../../../api/client';
import {
  conflictOf,
  type ReorderRequest,
  type WorkItemChanges,
  type WriteResult,
} from '../../../api/workItems';

const NO_CHANGES: WorkItemChanges = {
  title: null,
  description: null,
  acceptanceCriteria: null,
  reproSteps: null,
  severity: null,
  state: null,
  assignee: null,
  sprint: null,
  priority: null,
  points: null,
  parent: null,
  tags: null,
  addLinks: [],
  removeLinkIds: [],
};

/**
 * The write for a drop in Planning: the sprint change (a PATCH, which bumps the revision; null sprint = the Backlog)
 * and then the reorder against the revision the PATCH returned. Either part is skipped when it is undefined.
 * `onWritten` gets the PATCH's answer as soon as it arrives, so the caller can put the new revision into the cache
 * even when the reorder fails.
 *
 * The returned write is sent again by Use mine. Once the PATCH has landed it is not sent again: a reorder that is stale
 * is resent alone. A reorder that fails for any other reason after the PATCH throws `notSaved`, so the user is told the
 * item did move.
 */
export const sprintMoveWrite = (
  itemId: string,
  sprintId: string | null | undefined,
  beforeItemId: string | null | undefined,
  onWritten: (written: WriteResult) => void = () => undefined,
  notSaved?: string,
): ((revision: number) => Promise<WriteResult | undefined>) => {
  let patched = false;
  return async (revision) => {
    const id = encodeURIComponent(itemId);
    let expectedRevision = revision;
    let written: WriteResult | undefined;
    if (sprintId !== undefined && !patched) {
      written = await api.patch<WriteResult>(`/api/work-items/${id}`, {
        expectedRevision,
        changes: { ...NO_CHANGES, sprint: { value: sprintId } },
      });
      patched = true;
      onWritten(written);
      expectedRevision = written.revision;
    }
    if (beforeItemId !== undefined) {
      try {
        await api.post(`/api/work-items/${id}/reorder`, {
          expectedRevision,
          beforeItemId,
        } satisfies ReorderRequest);
      } catch (error) {
        if (patched && notSaved !== undefined && conflictOf(error) === undefined) {
          throw new Error(notSaved, { cause: error });
        }
        throw error;
      }
    }
    return written;
  };
};
