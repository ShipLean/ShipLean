import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { registerInvalidation } from '../../../realtime';
import { useWorkItemsOfType, workItemListKey } from '../../workItem/workItemApi';

const EPIC_EVENTS = ['workItemCreated', 'workItemUpdated', 'workItemRemoved'] as const;

/** The project's Epics, for the Epic filter; fetched while the filter bar is open and refetched when Epics change. */
export function useEpicOptions(projectId: string, enabled: boolean) {
  const queryClient = useQueryClient();
  useEffect(() => {
    if (!enabled) return undefined;
    const refetch = () =>
      void queryClient.invalidateQueries({ queryKey: workItemListKey(projectId, 'epic') });
    const unregister = EPIC_EVENTS.map((type) => registerInvalidation(type, refetch));
    return () => unregister.forEach((off) => off());
  }, [projectId, enabled, queryClient]);
  const epics = useWorkItemsOfType(projectId, 'epic', enabled);
  return (epics.data ?? []).map((epic) => ({ id: epic.id, title: epic.title }));
}
