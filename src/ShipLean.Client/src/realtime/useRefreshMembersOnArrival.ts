import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { projectMembersKey } from '../queries/shared';

/**
 * Refetches the project's members when someone new appears on the screen. A teammate may have changed their photo or
 * name since the members were cached, and nothing else tells this tab, so their arrival is the cue. Leaving is not.
 */
export function useRefreshMembersOnArrival(
  projectId: string | undefined,
  userIds: readonly string[],
): void {
  const queryClient = useQueryClient();
  const present = [...userIds].sort().join(',');
  const seen = useRef(new Set(userIds));
  useEffect(() => {
    const now = present === '' ? [] : present.split(',');
    const arrived = now.some((id) => !seen.current.has(id));
    seen.current = new Set(now);
    if (!arrived || projectId === undefined) return;
    void queryClient.invalidateQueries({ queryKey: projectMembersKey(projectId) });
  }, [queryClient, projectId, present]);
}
