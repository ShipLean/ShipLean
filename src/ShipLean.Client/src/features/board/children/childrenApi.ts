import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../api/client';
import type { WorkItemTypeId } from '../../../api/refs';
import type { ChildSummaryDto, WorkItemCreate, WriteResult } from '../../../api/workItems';
import { registerInvalidation } from '../../../realtime';
import { boardRootKey } from '../boardApi';
import { movesPending } from '../moves/pendingMoves';

// The children a roll-up lists live outside the board's root key: moves rewrite every query under that key, and a
// list of children is not a board. The open list refetches on the events that change the board.
export const childrenRootKey = (projectId: string) =>
  ['projects', projectId, 'board-children'] as const;
export const childrenKey = (projectId: string, itemId: string, type: WorkItemTypeId) =>
  [...childrenRootKey(projectId), itemId, type] as const;

const CHILD_EVENTS = [
  'projectChanged',
  'workItemCreated',
  'workItemUpdated',
  'workItemMoved',
  'workItemRemoved',
  'statesChanged',
] as const;

/** Everything below the card of the type the roll-up counts (nested Features included), open ones first. */
export function useChildren(projectId: string, itemId: string, type: WorkItemTypeId) {
  const queryClient = useQueryClient();
  useEffect(() => {
    const refetch = () =>
      void queryClient.invalidateQueries({ queryKey: childrenRootKey(projectId) });
    const unregister = CHILD_EVENTS.map((event) => registerInvalidation(event, refetch));
    return () => unregister.forEach((off) => off());
  }, [projectId, queryClient]);
  return useQuery({
    queryKey: childrenKey(projectId, itemId, type),
    queryFn: () =>
      api.get<ChildSummaryDto[]>(
        `/api/work-items/${encodeURIComponent(itemId)}/children?type=${encodeURIComponent(type)}`,
      ),
  });
}

/** Creates a work item of one type under the card; the board and the list refetch (after the last move in flight). */
export function useCreateChild(projectId: string, boardId: string, parentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ type, title }: { type: WorkItemCreate['type']; title: string }) =>
      api.post<WriteResult>('/api/work-items', {
        projectId,
        type,
        title,
        parentId,
        sprintId: null,
        changes: null,
      } satisfies WorkItemCreate),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: childrenRootKey(projectId) });
      return movesPending(boardId)
        ? undefined
        : queryClient.invalidateQueries({ queryKey: boardRootKey(projectId, boardId) });
    },
  });
}
