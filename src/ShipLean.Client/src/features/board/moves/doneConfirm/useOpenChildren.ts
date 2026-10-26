import { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../../api/client';
import type { ChildSummaryDto } from '../../../../api/workItems';
import { registerInvalidation } from '../../../../realtime';

/** Everything below a card that is neither completed nor removed. Tasks are no part of a Feature's or Epic's roll-ups. */
export const isOpenChild = (child: ChildSummaryDto) =>
  child.type.id !== 'task' &&
  child.state.category !== 'completed' &&
  child.state.category !== 'removed';

// Outside the board's own key: a board key holds board views, and a move writes into every one of them.
export const openChildrenKey = (projectId: string, itemId: string) =>
  ['projects', projectId, 'done-confirm', itemId] as const;

// What changes the children of a card: they are created, edited, moved, removed or given another state.
const CHILD_EVENTS = [
  'workItemCreated',
  'workItemUpdated',
  'workItemMoved',
  'workItemRemoved',
  'statesChanged',
] as const;

/** The children still open below a card, from the children endpoint. */
export function useOpenChildren(projectId: string, itemId: string) {
  const queryClient = useQueryClient();
  useEffect(() => {
    const refetch = () =>
      void queryClient.invalidateQueries({ queryKey: openChildrenKey(projectId, itemId) });
    const unregister = CHILD_EVENTS.map((type) => registerInvalidation(type, refetch));
    return () => unregister.forEach((off) => off());
  }, [projectId, itemId, queryClient]);
  return useQuery({
    queryKey: openChildrenKey(projectId, itemId),
    queryFn: () =>
      api.get<ChildSummaryDto[]>(`/api/work-items/${encodeURIComponent(itemId)}/children`),
    select: (children) => children.filter(isOpenChild),
  });
}
