import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ChildSummaryDto, WorkItemChanges } from '../../../api/workItems';
import { fetchWorkItem, patchWorkItem, workItemKey } from '../workItemApi';

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

// Moves one task to another state through the ordinary work item PATCH. The summary the panel holds has no revision, so the
// task is read first and the write carries the revision just read; the parent is refetched afterwards, since its
// revision does not move when a child's state does.
export function useTaskState(projectId: string, itemKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (change: { task: ChildSummaryDto; stateId: string }) => {
      const current = await fetchWorkItem(projectId, change.task.key);
      return patchWorkItem(current.id, {
        expectedRevision: current.revision,
        changes: { ...NO_CHANGES, state: { value: change.stateId } },
      });
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: workItemKey(projectId, itemKey) }),
  });
}
