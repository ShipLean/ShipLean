import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../api/client';
import type { InstanceProjectDto, InstanceSettingsDto } from '../../../api/projects';
import { registerInvalidation } from '../../../realtime';
import type { SettingsEdit } from './model';
import { instanceUsersKey } from '../instanceUsers/usersAdminApi';

export const instanceSettingsKey = ['instance', 'settings'] as const;
export const instanceProjectsKey = ['instance', 'projects'] as const;

const fetchSettings = () => api.get<InstanceSettingsDto>('/api/instance/settings');

/**
 * The instance settings document. Instance pages may have no open project, so no real-time event reaches them: the
 * query refetches when the window regains focus, and a save puts the document the server returned into the cache.
 */
export function useInstanceSettings() {
  return useQuery({
    queryKey: instanceSettingsKey,
    queryFn: fetchSettings,
    refetchOnWindowFocus: true,
  });
}

export function useSaveInstanceSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    // Only what the admin changed goes over the latest document, read again now, so a teammate's change to another
    // field since the last refetch isn't reverted.
    mutationFn: async (edit: SettingsEdit) => {
      const latest = await queryClient.fetchQuery({
        queryKey: instanceSettingsKey,
        queryFn: fetchSettings,
        staleTime: 0,
      });
      return api.put<InstanceSettingsDto>('/api/instance/settings', { ...latest, ...edit });
    },
    onSuccess: (saved) => {
      queryClient.setQueryData(instanceSettingsKey, saved);
    },
  });
}

/** Every project on the install. A member change in any open project refetches it; so does focus. */
export function useInstanceProjects() {
  const queryClient = useQueryClient();
  useEffect(
    () =>
      registerInvalidation('projectChanged', () => {
        void queryClient.invalidateQueries({ queryKey: instanceProjectsKey });
      }),
    [queryClient],
  );
  return useQuery({
    queryKey: instanceProjectsKey,
    queryFn: () => api.get<InstanceProjectDto[]>('/api/instance/projects'),
    refetchOnWindowFocus: true,
  });
}

/** Joining adds the caller as Admin, so every list that shows memberships is read again. */
export function useJoinAsAdmin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (projectId: string) =>
      api.post<void>(`/api/instance/projects/${encodeURIComponent(projectId)}/join`),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: instanceProjectsKey }),
        queryClient.invalidateQueries({ queryKey: instanceUsersKey }),
        queryClient.invalidateQueries({ queryKey: ['projects'] }),
        queryClient.invalidateQueries({ queryKey: ['me'] }),
      ]);
    },
  });
}
