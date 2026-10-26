import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { WorkItemDto } from '../../../api/workItems';
import { registerInvalidation } from '../../../realtime';
import { workItemKey } from '../workItemApi';

// Adding a child doesn't bump the parent's revision, so the parent's `children` go stale on their own: refetch the parent
// when a task is created under it, removed from it or moved onto it (an event carrying its id as `parentId`), when one of
// its children changes (a state move, a rename), and when any item's parent is edited (that event names no parent, so the
// gaining or losing item can't be told apart).
export function useChildEvents(projectId: string, itemKey: string, item: WorkItemDto | undefined) {
  const queryClient = useQueryClient();
  const id = item?.id;
  const childIds = (item?.children ?? []).map((child) => child.id).join(',');
  useEffect(() => {
    if (id === undefined) return undefined;
    const children = new Set(childIds === '' ? [] : childIds.split(','));
    const refetch = () => {
      void queryClient.invalidateQueries({ queryKey: workItemKey(projectId, itemKey) });
    };
    const ofParent = (event: { parentId: string | null }) => {
      if (event.parentId === id) refetch();
    };
    const ofMove = (event: { id: string; parentId: string | null }) => {
      if (children.has(event.id) || event.parentId === id) refetch();
    };
    const ofUpdate = (event: { id: string; fields: string[] }) => {
      if (children.has(event.id) || event.fields.includes('parent')) refetch();
    };
    const unregister = [
      registerInvalidation('workItemCreated', ofParent),
      registerInvalidation('workItemRemoved', ofParent),
      registerInvalidation('workItemUpdated', ofUpdate),
      registerInvalidation('workItemMoved', ofMove),
    ];
    return () => unregister.forEach((off) => off());
  }, [id, childIds, projectId, itemKey, queryClient]);
}
