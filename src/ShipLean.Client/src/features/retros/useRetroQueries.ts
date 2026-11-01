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
// retro view. A hook gives the key of its own query and what concerns it ('any' for a project-wide read), so an event
// refetches only the queries it touches and never the ones that merely share the retro prefix. Own writes invalidate
// directly (a tab ignores its own events).
type Concern = 'any' | { sprintId: string } | { workItemId: string };

function useRetroChanged(queryKey: readonly unknown[], concern: Concern) {
  const queryClient = useQueryClient();
  const sprintId = typeof concern === 'object' && 'sprintId' in concern ? concern.sprintId : null;
  const workItemId =
    typeof concern === 'object' && 'workItemId' in concern ? concern.workItemId : null;
  const keyText = JSON.stringify(queryKey);
  useEffect(
    () =>
      registerInvalidation('retroChanged', (event) => {
        const wide = event.sprintIds.length === 0 && event.workItemIds.length === 0;
        const touched =
          wide ||
          concern === 'any' ||
          (sprintId !== null && event.sprintIds.includes(sprintId)) ||
          (workItemId !== null && event.workItemIds.includes(workItemId));
        if (touched) void queryClient.invalidateQueries({ queryKey: JSON.parse(keyText) });
      }),
    // `concern` is rebuilt every render; sprintId, workItemId and the key text are what it is made of.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [queryClient, keyText, sprintId, workItemId, concern === 'any'],
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
  useRetroChanged([...retrosRootKey(projectId), 'search'], 'any');
  return useQuery({
    queryKey: retroSearchKey(projectId, query),
    queryFn: () => fetchRetroSearch(projectId, query),
    placeholderData: keepPreviousData,
  });
}

/** GET api/projects/{id}/retros/insights. */
export function useRetroInsights(projectId: string, range: RetroRange) {
  useRetroChanged([...retrosRootKey(projectId), 'insights'], 'any');
  return useQuery({
    queryKey: retroInsightsKey(projectId, range),
    queryFn: () => fetchRetroInsights(projectId, range),
  });
}

/** GET api/projects/{id}/retros/themes: the theme picker's list. */
export function useRetroThemes(projectId: string) {
  useRetroChanged(retroThemesKey(projectId), 'any');
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
