import { useMutation, useQueryClient } from '@tanstack/react-query';
import { showToast } from '../../../../components/toastStore';
import type { WorkItemChanges } from '../../../../api/workItems';
import { fetchWorkItem, patchWorkItem, workItemKey } from '../../../workItem/workItemApi';
import { retrosRootKey } from '../../useRetroQueries';

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

// Puts an action item into a sprint through the ordinary work item PATCH. The row carries no revision, so the item is read
// first and the write carries the revision just read; the insights are refetched whatever the outcome.
export function usePullIntoSprint(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (pull: { itemKey: string; sprintId: string }) => {
      const current = await fetchWorkItem(projectId, pull.itemKey);
      return patchWorkItem(current.id, {
        expectedRevision: current.revision,
        changes: { ...NO_CHANGES, sprint: { value: pull.sprintId } },
      });
    },
    onError: (failure) => showToast(failure.message, { tone: 'danger' }),
    onSettled: (_result, _failure, pull) =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: retrosRootKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: workItemKey(projectId, pull.itemKey) }),
      ]),
  });
}
