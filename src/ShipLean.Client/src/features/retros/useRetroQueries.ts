import { useEffect } from 'react';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  fetchRetroInsights,
  fetchRetroSearch,
  fetchRetroThemes,
  fetchSprintRetro,
  fetchWorkItemRetros,
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
export const workItemRetrosKey = (projectId: string, workItemId: string) =>
  [...retrosRootKey(projectId), 'work-item', workItemId] as const;

// A retroChanged event names the sprints and work items it touched; empty lists (a theme rename or merge) mean every
// retro view. Own writes invalidate directly (a tab ignores its own events).
function useRetroChanged(
  projectId: string,
  kind: 'project' | 'themes' | 'sprint' | 'item',
  id = '',
) {
  const queryClient = useQueryClient();
  useEffect(
    () =>
      registerInvalidation('retroChanged', (event) => {
        const wide = event.sprintIds.length === 0 && event.workItemIds.length === 0;
        const touched =
          wide ||
          kind === 'project' ||
          kind === 'themes' ||
          (kind === 'sprint' && event.sprintIds.includes(id)) ||
          (kind === 'item' && event.workItemIds.includes(id));
        if (!touched) return;
        let queryKey: readonly unknown[] = retrosRootKey(projectId);
        if (kind === 'themes') queryKey = retroThemesKey(projectId);
        else if (kind === 'sprint') queryKey = sprintRetroKey(projectId, id);
        else if (kind === 'item') queryKey = workItemRetrosKey(projectId, id);
        void queryClient.invalidateQueries({ queryKey });
      }),
    [projectId, queryClient, kind, id],
  );
}

/** GET api/sprints/{id}/retro, refetched when a retro of that sprint changes. */
export function useSprintRetro(projectId: string, sprintId: string) {
  useRetroChanged(projectId, 'sprint', sprintId);
  return useQuery({
    queryKey: sprintRetroKey(projectId, sprintId),
    queryFn: () => fetchSprintRetro(sprintId),
  });
}

/** GET api/projects/{id}/retros: the previous result stays on screen while a new filter loads. */
export function useRetroSearch(projectId: string, query: RetroSearchQuery) {
  useRetroChanged(projectId, 'project');
  return useQuery({
    queryKey: retroSearchKey(projectId, query),
    queryFn: () => fetchRetroSearch(projectId, query),
    placeholderData: keepPreviousData,
  });
}

/** GET api/projects/{id}/retros/insights. */
export function useRetroInsights(projectId: string, range: RetroRange, enabled = true) {
  useRetroChanged(projectId, 'project');
  return useQuery({
    queryKey: retroInsightsKey(projectId, range),
    queryFn: () => fetchRetroInsights(projectId, range),
    enabled,
  });
}

/** GET api/projects/{id}/retros/themes: the theme picker's list. */
export function useRetroThemes(projectId: string) {
  useRetroChanged(projectId, 'themes');
  return useQuery({
    queryKey: retroThemesKey(projectId),
    queryFn: () => fetchRetroThemes(projectId),
  });
}

/** GET api/work-items/{id}/retros, refetched when a retro of that item changes. */
export function useWorkItemRetros(projectId: string, workItemId: string) {
  useRetroChanged(projectId, 'item', workItemId);
  return useQuery({
    queryKey: workItemRetrosKey(projectId, workItemId),
    queryFn: () => fetchWorkItemRetros(workItemId),
  });
}
