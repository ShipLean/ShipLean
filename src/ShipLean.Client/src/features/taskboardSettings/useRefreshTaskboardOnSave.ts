import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { taskboardKey } from '../taskboard/taskboardApi';

/**
 * A save puts the document the server returned into the project's board settings cache (a manual write; a plain
 * fetch is not one). This tab drops its own `boardSettingsChanged` event, so that write refreshes the open Taskboard.
 */
export function useRefreshTaskboardOnSave(projectId: string, sprintId: string) {
  const queryClient = useQueryClient();
  useEffect(
    () =>
      queryClient.getQueryCache().subscribe((event) => {
        const [root, id, collection, , leaf] = event.query.queryKey;
        const isSettings =
          root === 'projects' && id === projectId && collection === 'boards' && leaf === 'settings';
        if (
          isSettings &&
          event.type === 'updated' &&
          event.action.type === 'success' &&
          event.action.manual
        ) {
          void queryClient.invalidateQueries({ queryKey: taskboardKey(projectId, sprintId) });
        }
      }),
    [queryClient, projectId, sprintId],
  );
}
