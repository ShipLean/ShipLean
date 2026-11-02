import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { WorkItemDto } from '../../../api/workItems';
import { useInvalidateBacklog } from '../useInvalidateBacklog';
import { createWorkItem, workItemKey } from '../workItemApi';

// Creates a task under the item through the ordinary work item create, then refetches the parent (its revision does not
// move, so nothing else would) and the backlog behind the modal.
export function useAddTask(projectId: string, itemKey: string, parent: WorkItemDto) {
  const queryClient = useQueryClient();
  const invalidateBacklog = useInvalidateBacklog();
  return useMutation({
    mutationFn: (title: string) =>
      createWorkItem({
        projectId,
        type: 'task',
        title,
        parentId: parent.id,
        sprintId: null,
        changes: null,
      }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: workItemKey(projectId, itemKey) }),
        invalidateBacklog(projectId),
      ]),
  });
}
