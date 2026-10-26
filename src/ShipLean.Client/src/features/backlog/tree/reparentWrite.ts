import { api } from '../../../api/client';
import type { ReorderRequest, WorkItemChanges, WriteResult } from '../../../api/workItems';

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
 * The write for a drop in the tree: the parent change (a PATCH, which bumps the revision) and then, for a drop on a
 * story, the reorder against the revision the PATCH returned. Either part is skipped when it is undefined. `onWritten`
 * is called with the PATCH's answer as soon as it arrives, so the caller can put the new revision into the cache even
 * when the reorder that follows fails. Resolves to that answer (undefined when there was no PATCH).
 *
 * A failure of the reorder after the PATCH succeeded leaves the new parent at the old position: the two writes are not
 * atomic, and one write that carries the parent and the position together needs a server change.
 */
export const reparentWrite =
  (
    itemId: string,
    parentId: string | null | undefined,
    beforeItemId: string | undefined,
    onWritten: (written: WriteResult) => void = () => undefined,
  ): ((revision: number) => Promise<WriteResult | undefined>) =>
  async (revision) => {
    const id = encodeURIComponent(itemId);
    let expectedRevision = revision;
    let written: WriteResult | undefined;
    if (parentId !== undefined) {
      written = await api.patch<WriteResult>(`/api/work-items/${id}`, {
        expectedRevision,
        changes: { ...NO_CHANGES, parent: { value: parentId } },
      });
      onWritten(written);
      expectedRevision = written.revision;
    }
    if (beforeItemId !== undefined) {
      await api.post(`/api/work-items/${id}/reorder`, {
        expectedRevision,
        beforeItemId,
      } satisfies ReorderRequest);
    }
    return written;
  };
