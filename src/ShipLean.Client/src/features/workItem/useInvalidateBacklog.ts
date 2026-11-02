import { useQueryClient } from '@tanstack/react-query';
import { backlogRootKey } from '../../queries/backlogKeys';

// Refetches every backlog read of the project (all levels and modes). A tab ignores its own events, so a save in the
// item modal over a Backlogs page would otherwise leave the row behind it stale, with a revision that makes the next
// bulk write stale too. Every work item save, state change and task add calls it.
export function useInvalidateBacklog(): (projectId: string) => Promise<void> {
  const queryClient = useQueryClient();
  return (projectId) => queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) });
}
