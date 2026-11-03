import { useEffect } from 'react';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  fetchRetroAction,
  fetchRetroActionDialog,
  fetchRetroFeatures,
  fetchRetroInsights,
  fetchRetroItem,
  fetchRetroSearch,
  fetchRetroThemes,
  fetchSprintRetro,
  fetchWorkItemRetros,
  type RetroActionDialogQuery,
  type RetroRange,
  type RetroSearchQuery,
} from '../../api/retrosApi';
import { registerInvalidation } from '../../realtime';

// Every retrospective read lives under ['projects', projectId, 'retros'], so a reconnect refreshes them with the rest.
export const retrosRootKey = (projectId: string) => ['projects', projectId, 'retros'] as const;
export const sprintRetroKey = (projectId: string, sprintId: string) =>
  [...retrosRootKey(projectId), 'sprint', sprintId] as const;
export const retroSearchKey = (projectId: string, query: RetroSearchQuery) =>
  [...retrosRootKey(projectId), 'search', query] as const;
export const retroInsightsKey = (projectId: string, range: RetroRange) =>
  [...retrosRootKey(projectId), 'insights', range] as const;
export const retroThemesKey = (projectId: string) =>
  [...retrosRootKey(projectId), 'themes'] as const;
export const retroFeaturesKey = (projectId: string) =>
  [...retrosRootKey(projectId), 'features'] as const;
export const retroItemKey = (projectId: string, itemKey: string) =>
  [...retrosRootKey(projectId), 'item', itemKey] as const;
export const retroActionDialogKey = (projectId: string, query: RetroActionDialogQuery) =>
  [...retrosRootKey(projectId), 'action-dialog', query] as const;
export const retroActionKey = (projectId: string, workItemId: string) =>
  [...retrosRootKey(projectId), 'action', workItemId] as const;
export const workItemRetrosKey = (projectId: string, workItemId: string) =>
  [...retrosRootKey(projectId), 'work-item', workItemId] as const;

// A retroChanged event names the sprints and work items it touched; empty lists (a theme rename or merge) mean every
// retro view. A hook gives the key of its own query and the sprint or work item it concerns (neither: any event), so an
// event refetches only the queries it touches and never the ones that merely share the retro prefix. Own writes
// invalidate directly (a tab ignores its own events).
function useRetroChanged(
  queryKey: readonly unknown[],
  concern: { sprintId?: string; workItemId?: string } = {},
) {
  const queryClient = useQueryClient();
  const { sprintId, workItemId } = concern;
  const keyText = JSON.stringify(queryKey);
  useEffect(
    () =>
      registerInvalidation('retroChanged', (event) => {
        const wide = event.sprintIds.length === 0 && event.workItemIds.length === 0;
        const any = sprintId === undefined && workItemId === undefined;
        const touched =
          wide ||
          any ||
          (sprintId !== undefined && event.sprintIds.includes(sprintId)) ||
          (workItemId !== undefined && event.workItemIds.includes(workItemId));
        if (touched) {
          void queryClient.invalidateQueries({ queryKey: JSON.parse(keyText) as unknown[] });
        }
      }),
    [queryClient, keyText, sprintId, workItemId],
  );
}

/** GET api/sprints/{id}/retro, refetched when a retro of that sprint changes. */
export function useSprintRetro(projectId: string, sprintId: string) {
  useRetroChanged(sprintRetroKey(projectId, sprintId), { sprintId });
  return useQuery({
    queryKey: sprintRetroKey(projectId, sprintId),
    queryFn: () => fetchSprintRetro(sprintId),
  });
}

/** GET api/projects/{id}/retros: the previous result stays on screen while a new filter loads. */
export function useRetroSearch(projectId: string, query: RetroSearchQuery) {
  useRetroChanged([...retrosRootKey(projectId), 'search']);
  return useQuery({
    queryKey: retroSearchKey(projectId, query),
    queryFn: () => fetchRetroSearch(projectId, query),
    placeholderData: keepPreviousData,
  });
}

/** GET api/projects/{id}/retros/insights. */
export function useRetroInsights(projectId: string, range: RetroRange) {
  useRetroChanged([...retrosRootKey(projectId), 'insights']);
  return useQuery({
    queryKey: retroInsightsKey(projectId, range),
    queryFn: () => fetchRetroInsights(projectId, range),
  });
}

/** GET api/projects/{id}/retros/themes: the theme picker's list. */
export function useRetroThemes(projectId: string) {
  useRetroChanged(retroThemesKey(projectId));
  return useQuery({
    queryKey: retroThemesKey(projectId),
    queryFn: () => fetchRetroThemes(projectId),
  });
}

/** GET api/work-items/{id}/retros, refetched when a retro of that item changes. */
export function useWorkItemRetros(projectId: string, workItemId: string) {
  useRetroChanged(workItemRetrosKey(projectId, workItemId), { workItemId });
  return useQuery({
    queryKey: workItemRetrosKey(projectId, workItemId),
    queryFn: () => fetchWorkItemRetros(workItemId),
  });
}

/** GET api/projects/{id}/retros/features. Any retro change can move a count, so every event refetches it. */
export function useRetroFeatures(projectId: string) {
  useRetroChanged(retroFeaturesKey(projectId));
  return useQuery({
    queryKey: retroFeaturesKey(projectId),
    queryFn: () => fetchRetroFeatures(projectId),
  });
}

/** GET api/projects/{id}/retros/items/{key}: one item's retro screen. */
export function useRetroItem(projectId: string, itemKey: string) {
  useRetroChanged(retroItemKey(projectId, itemKey));
  return useQuery({
    queryKey: retroItemKey(projectId, itemKey),
    queryFn: () => fetchRetroItem(projectId, itemKey),
  });
}

/** GET api/projects/{id}/retros/action-dialog: what the action dialog offers for the selection it opened on. */
export function useRetroActionDialogData(
  projectId: string,
  query: RetroActionDialogQuery,
  enabled = true,
) {
  useRetroChanged([...retrosRootKey(projectId), 'action-dialog']);
  return useQuery({
    queryKey: retroActionDialogKey(projectId, query),
    queryFn: () => fetchRetroActionDialog(projectId, query),
    enabled,
  });
}

/** GET api/retros/actions/{workItemId}: an action item with the points it covers. */
export function useRetroAction(projectId: string, workItemId: string, enabled = true) {
  useRetroChanged(retroActionKey(projectId, workItemId));
  return useQuery({
    queryKey: retroActionKey(projectId, workItemId),
    queryFn: () => fetchRetroAction(workItemId),
    enabled,
  });
}
