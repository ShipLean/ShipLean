// The demo hooks. Every demo read lives under ['projects', projectId, 'demos'], so a reconnect refreshes them with the
// rest. Own writes set or invalidate directly (a tab ignores its own events).
import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  checkYouTube,
  createSprintDemo,
  disconnectYouTube,
  fetchDemoSettings,
  fetchSprintDemo,
  removeDemoLogo,
  retryYouTube,
  saveDemoSettings,
  saveDemoTokens,
  startYouTube,
  uploadDemoLogo,
  type DemoTokensPut,
  type ProjectDemoSettingsDto,
  type ProjectDemoSettingsPut,
  type SprintDemoDto,
  type YouTubeCheckRequest,
  type YouTubeDisconnectDto,
  type YouTubeProjectDto,
} from '../../api/demos';
import { registerInvalidation } from '../../realtime';

export const demosRootKey = (projectId: string) => ['projects', projectId, 'demos'] as const;
export const sprintDemoKey = (projectId: string, sprintId: string) =>
  [...demosRootKey(projectId), 'sprint', sprintId] as const;
export const demoSettingsKey = (projectId: string) =>
  [...demosRootKey(projectId), 'settings'] as const;

export function useSprintDemo(projectId: string, sprintId: string) {
  return useQuery({
    queryKey: sprintDemoKey(projectId, sprintId),
    queryFn: () => fetchSprintDemo(sprintId),
  });
}

// A teammate's demo write, the demo item's agent session or its attachments changed: read the demo again. The agent
// and attachment events name a work item and the demo's item has no id on the DTO, so while the sprint has a run any
// such event refetches.
export function useSprintDemoRealtime(projectId: string, sprintId: string) {
  const queryClient = useQueryClient();
  useEffect(() => {
    const key = sprintDemoKey(projectId, sprintId);
    const refetch = () => void queryClient.invalidateQueries({ queryKey: key });
    const whileRunning = () => {
      if (queryClient.getQueryData<SprintDemoDto>(key)?.run != null) refetch();
    };
    const unregister = [
      registerInvalidation('sprintDemoChanged', (event) => {
        if (event.sprintId === sprintId) refetch();
      }),
      registerInvalidation('agentSessionChanged', whileRunning),
      registerInvalidation('attachmentsChanged', whileRunning),
    ];
    return () => unregister.forEach((off) => off());
  }, [projectId, sprintId, queryClient]);
}

// The write hooks take the sprint alone, as the components that call them are given only the demo (its sprintId). The
// answer replaces every cached read of that sprint's demo, wherever its project is rooted.
const isSprintDemoKey = (sprintId: string) => (key: readonly unknown[]) =>
  key[0] === 'projects' && key[2] === 'demos' && key[3] === 'sprint' && key[4] === sprintId;

function useSprintDemoWrite(sprintId: string, write: (sprintId: string) => Promise<SprintDemoDto>) {
  const queryClient = useQueryClient();
  const matches = isSprintDemoKey(sprintId);
  return useMutation({
    mutationFn: () => write(sprintId),
    onSuccess: (demo) =>
      queryClient.setQueriesData({ predicate: (query) => matches(query.queryKey) }, demo),
    // A refusal usually means the page was stale (a live session, a state that moved): read it again.
    onError: () => queryClient.invalidateQueries({ predicate: (query) => matches(query.queryKey) }),
  });
}

export const useCreateSprintDemo = (sprintId: string) =>
  useSprintDemoWrite(sprintId, createSprintDemo);

export const useRetryYouTube = (sprintId: string) => useSprintDemoWrite(sprintId, retryYouTube);

export function useDemoSettings(projectId: string) {
  return useQuery({
    queryKey: demoSettingsKey(projectId),
    queryFn: () => fetchDemoSettings(projectId),
  });
}

export function useSaveDemoSettings(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (put: ProjectDemoSettingsPut) => saveDemoSettings(projectId, put),
    onSuccess: (settings) => queryClient.setQueryData(demoSettingsKey(projectId), settings),
  });
}

// The brand writes answer the brand alone: fold it into the cached settings.
function useBrandWrite<V>(
  projectId: string,
  write: (projectId: string, variables: V) => Promise<ProjectDemoSettingsDto['brand']>,
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: V) => write(projectId, variables),
    onSuccess: (brand) =>
      queryClient.setQueryData<ProjectDemoSettingsDto>(demoSettingsKey(projectId), (settings) =>
        settings === undefined ? settings : { ...settings, brand },
      ),
  });
}

export const useUploadDemoLogo = (projectId: string) =>
  useBrandWrite<File>(projectId, uploadDemoLogo);
export const useRemoveDemoLogo = (projectId: string) =>
  useBrandWrite<void>(projectId, (id) => removeDemoLogo(id));
export const useSaveDemoTokens = (projectId: string) =>
  useBrandWrite<DemoTokensPut>(projectId, saveDemoTokens);

// The YouTube writes change the connection, which lives on the settings.
function useYouTubeWrite<V, R>(
  projectId: string,
  write: (projectId: string, variables: V) => Promise<R>,
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (variables: V) => write(projectId, variables),
    onSettled: () => queryClient.invalidateQueries({ queryKey: demoSettingsKey(projectId) }),
  });
}

export const useStartYouTube = (projectId: string) =>
  useYouTubeWrite<void, { url: string }>(projectId, (id) => startYouTube(id));
export const useCheckYouTube = (projectId: string) =>
  useYouTubeWrite<YouTubeCheckRequest, YouTubeProjectDto>(projectId, checkYouTube);
export const useDisconnectYouTube = (projectId: string) =>
  useYouTubeWrite<void, YouTubeDisconnectDto>(projectId, (id) => disconnectYouTube(id));
