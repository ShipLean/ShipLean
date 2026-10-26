import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { ApiRequestError, api } from '../../../api/client';
import type { KeyCheckDto, ProjectCreate, ProjectDto } from '../../../api/projects';
import type { SprintCreate } from '../../../api/sprints';
import { registerInvalidation } from '../../../realtime';
import { fetchMe, fetchProjects, meKey, projectsKey } from '../../../queries/shared';
import { keyIsWellFormed } from './model';

// Waiting for a pause in typing; a test waits for the request, not for this.
const TYPING_PAUSE_MS = 250;

export const keyCheckKey = (key: string, name: string) =>
  ['newProject', 'key-check', key, name] as const;

const checkKey = (key: string, name: string) =>
  api.get<KeyCheckDto>(
    `/api/projects/key-check?key=${encodeURIComponent(key)}&name=${encodeURIComponent(name)}`,
  );

/** The live check: asks once the key has stopped changing and looks like a key. `settled` says the answer is for this key. */
export function useKeyCheck(key: string, name: string) {
  const [asked, setAsked] = useState({ key, name });
  useEffect(() => {
    const timer = setTimeout(() => setAsked({ key, name }), TYPING_PAUSE_MS);
    return () => clearTimeout(timer);
  }, [key, name]);
  const query = useQuery({
    queryKey: keyCheckKey(asked.key, asked.name),
    queryFn: () => checkKey(asked.key, asked.name),
    enabled: keyIsWellFormed(asked.key),
  });
  const settled = asked.key === key && query.data?.key === key;
  return { check: settled ? query.data : undefined, pending: keyIsWellFormed(key) && !settled };
}

export const firstSprintKey = (workingDays: number[]) =>
  ['newProject', 'first-sprint', workingDays.join(',')] as const;

/** Sprint 1 as the server would start it: today or the next working day, two weeks long. */
export function useFirstSprint(workingDays: number[], enabled: boolean) {
  return useQuery({
    queryKey: firstSprintKey(workingDays),
    queryFn: () =>
      api.get<SprintCreate>(`/api/projects/first-sprint?workingDays=${workingDays.join(',')}`),
    enabled: enabled && workingDays.length > 0,
  });
}

export const useMeForNewProject = () => useQuery({ queryKey: meKey, queryFn: fetchMe });

/** The projects you can copy from. A teammate changing a project (its name, your role) changes the list. */
export function useCopySources() {
  const queryClient = useQueryClient();
  useEffect(
    () =>
      registerInvalidation('projectChanged', () => {
        void queryClient.invalidateQueries({ queryKey: projectsKey });
      }),
    [queryClient],
  );
  return useQuery({ queryKey: projectsKey, queryFn: fetchProjects });
}

/** Creates the project, then drops the lists it joins (the picker, the sign-in landing) and the keys it used. */
export function useCreateProject(onCreated: (project: ProjectDto) => void) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (create: ProjectCreate) => api.post<ProjectDto>('/api/projects', create),
    // Someone took the key between the check and Create: the cached "available" is wrong, so ask again.
    onError: async (error, create) => {
      if (error instanceof ApiRequestError && error.status === 409) {
        await queryClient.resetQueries({
          queryKey: keyCheckKey(create.key, create.name),
          exact: true,
        });
      }
    },
    onSuccess: async (project) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: projectsKey }),
        queryClient.invalidateQueries({ queryKey: meKey }),
        queryClient.invalidateQueries({ queryKey: ['newProject'] }),
      ]);
      onCreated(project);
    },
  });
}
