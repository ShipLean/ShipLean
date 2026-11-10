import { useQuery, useQueryClient } from '@tanstack/react-query';
import { asksKeys, getItemAsks } from '@shiplean/core/api/asks';
import { commentKeys, listComments } from '@shiplean/core/api/commentsApi';
import { featureBoardKeys, getFeatureBoard } from '@shiplean/core/api/featureBoard';
import { getHistory, historyKeys } from '@shiplean/core/api/history';
import { getMembers, getStates, projectMetaKeys } from '@shiplean/core/api/projectMeta';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import { getChildren, getItemByKey, workItemKeys } from '@shiplean/core/api/workItem';

export function useWorkItemQuery(projectId: string, itemKey: string) {
  return useQuery({
    queryKey: workItemKeys.byKey(projectId, itemKey),
    queryFn: () => getItemByKey(projectId, itemKey),
  });
}

// Keyed as the SPA keys it, under the work item's own key. The id is what the fetch needs.
export function useItemAsksQuery(projectId: string, itemKey: string, itemId: string) {
  return useQuery({
    queryKey: asksKeys.item(projectId, itemKey),
    queryFn: () => getItemAsks(itemId),
  });
}

export function useCommentsQuery(itemId: string) {
  return useQuery({
    queryKey: commentKeys.list(itemId),
    queryFn: () => listComments(itemId),
  });
}

export function useHistoryQuery(itemId: string) {
  return useQuery({
    queryKey: historyKeys.item(itemId),
    queryFn: () => getHistory(itemId),
  });
}

export function useChildrenQuery(itemId: string, type?: WorkItemTypeId) {
  return useQuery({
    queryKey: [...workItemKeys.children(itemId), type ?? 'all'],
    queryFn: () => getChildren(itemId, type),
  });
}

// Shares its key with the Features lane's board query, so a change in one area refreshes the other.
export function useFeatureBoardQuery(projectId: string, featureKey: string) {
  return useQuery({
    queryKey: featureBoardKeys.board(projectId, featureKey),
    queryFn: () => getFeatureBoard(projectId, featureKey),
  });
}

// The project's states for one type, in the order the server sends them.
export function useTypeStatesQuery(projectId: string, typeId: WorkItemTypeId) {
  return useQuery({
    queryKey: projectMetaKeys.states(projectId),
    queryFn: () => getStates(projectId),
    select: (states) => states.filter((state) => state.type === typeId),
  });
}

export function useMembersQuery(projectId: string) {
  return useQuery({
    queryKey: projectMetaKeys.members(projectId),
    queryFn: () => getMembers(projectId),
  });
}

// Everything the Work item screen reads for one item: the item and its asks (both under its by-key key), comments,
// history, children and the Feature boards.
export function useInvalidateWorkItem() {
  const client = useQueryClient();
  return async (projectId: string, itemKey: string, itemId: string): Promise<void> => {
    await Promise.all([
      client.invalidateQueries({ queryKey: workItemKeys.byKey(projectId, itemKey) }),
      client.invalidateQueries({ queryKey: commentKeys.list(itemId) }),
      client.invalidateQueries({ queryKey: historyKeys.item(itemId) }),
      client.invalidateQueries({ queryKey: workItemKeys.children(itemId) }),
      // Every board of the project: the factory's key without its Feature.
      client.invalidateQueries({
        queryKey: featureBoardKeys.board(projectId, itemKey).slice(0, 3),
      }),
    ]);
  };
}
