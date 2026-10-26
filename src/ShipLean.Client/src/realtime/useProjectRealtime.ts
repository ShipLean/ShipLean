import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { realtime } from './realtime';
import { useProjectByKey } from './useProjectByKey';

// Joins the project the key names for as long as the caller is mounted: resolves the key (the same query every screen
// shares), joins, and leaves when the key changes or the caller unmounts. After a reconnect every query of the
// project is invalidated, so what is on screen is read again.
export function useProjectRealtime(projectKey: string | undefined): void {
  const queryClient = useQueryClient();
  const { data } = useProjectByKey(projectKey);
  const projectId = data?.id;

  useEffect(() => {
    realtime.setInvalidator((id) => {
      const queryKey = ['projects', id];
      const count = queryClient.getQueryCache().findAll({ queryKey }).length;
      void queryClient.invalidateQueries({ queryKey });
      return count;
    });
  }, [queryClient]);

  useEffect(() => {
    if (projectId === undefined) return;
    realtime.setProject(projectId);
    return () => realtime.setProject(undefined);
  }, [projectId]);
}
