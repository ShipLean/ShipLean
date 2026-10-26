import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { WorkItemDto } from '../../../api/workItems';
import { createWorkItem, workItemKey } from '../workItemApi';

// Creates a task under the item through the ordinary work item create, then refetches the parent (its revision does not
// move, so nothing else would).
export function useAddTask(projectId: string, itemKey: string, parent: WorkItemDto) {
  const queryClient = useQueryClient();
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
    onSuccess: () => queryClient.invalidateQueries({ queryKey: workItemKey(projectId, itemKey) }),
  });
}
