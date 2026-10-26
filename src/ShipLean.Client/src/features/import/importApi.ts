import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type {
  ImportJobDto,
  ImportMappingDto,
  JiraConnectionDto,
  JiraConnectionPut,
  JiraProjectDto,
  JiraTestResultDto,
} from '../../api/import';
import type { KeyCheckDto, ProjectCreate, ProjectDto } from '../../api/projects';
import { registerInvalidation } from '../../realtime';

// The Jira import's react-query keys and calls. Project data is keyed under ['projects', projectId, 'import', …].
export const importConnectionKey = (projectId: string) =>
  ['projects', projectId, 'import', 'connection'] as const;
export const jiraProjectsKey = (projectId: string) =>
  ['projects', projectId, 'import', 'jira-projects'] as const;
export const importJobsKey = (projectId: string) =>
  ['projects', projectId, 'import', 'jobs'] as const;
export const allImportJobsKey = ['import', 'jobs'] as const;
export const importMappingKey = (
  projectId: string,
  jiraProjectKey: string,
  previousJobId: string | null,
) => ['projects', projectId, 'import', 'mapping', jiraProjectKey, previousJobId] as const;
export const keyCheckKey = (key: string) => ['import', 'key-check', key] as const;

/** The body of POST api/import/mapping; previousJobId keeps a finished job's choices for a re-run. */
export interface ImportMappingRequest {
  connectionId: string | null;
  jiraProjectKey: string;
  previousJobId: string | null;
}
/** The bodies of the job actions; a null errorId acts on every pending error of the job. */
export interface ImportRetryRequest {
  errorId: string | null;
}
export interface ImportSkipRequest {
  errorId: string | null;
}
/** A null mapping re-runs with the mapping the job has saved. */
export interface ImportRerunRequest {
  mapping: ImportMappingDto | null;
}

const base = (projectId: string) => `/api/projects/${encodeURIComponent(projectId)}`;

// 204 when no connection is saved: api/client.ts hands that back as undefined, which react-query refuses to hold.
export const fetchConnection = async (projectId: string) =>
  (await api.get<JiraConnectionDto | undefined>(`${base(projectId)}/jira/connection`)) ?? null;
export const saveConnection = (projectId: string, put: JiraConnectionPut) =>
  api.put<JiraConnectionDto>(`${base(projectId)}/jira/connection`, put);
export const testConnection = (projectId: string) =>
  api.post<JiraTestResultDto>(`${base(projectId)}/jira/connection/test`);
export const fetchJiraProjects = (projectId: string) =>
  api.get<JiraProjectDto[]>(`${base(projectId)}/jira/projects`);
export const fetchImportJobs = (projectId: string) =>
  api.get<ImportJobDto[]>(`${base(projectId)}/import/jobs`);
export const fetchAllImportJobs = () => api.get<ImportJobDto[]>('/api/import/jobs');
export const fetchMapping = (request: ImportMappingRequest) =>
  api.post<ImportMappingDto>('/api/import/mapping', request);
export const fetchKeyCheck = (key: string) =>
  api.get<KeyCheckDto>(`/api/projects/key-check?key=${encodeURIComponent(key)}`);
export const createProject = (create: ProjectCreate) =>
  api.post<ProjectDto>('/api/projects', create);

export const useJiraConnection = (projectId: string) =>
  useQuery({
    queryKey: importConnectionKey(projectId),
    queryFn: () => fetchConnection(projectId),
  });

export const useJiraProjects = (projectId: string, enabled: boolean) =>
  useQuery({
    queryKey: jiraProjectsKey(projectId),
    queryFn: () => fetchJiraProjects(projectId),
    enabled,
  });

/** Every import job of the projects the caller administers, newest first. */
export const useAllImportJobs = () =>
  useQuery({ queryKey: allImportJobsKey, queryFn: fetchAllImportJobs });

export const useKeyCheck = (key: string) =>
  useQuery({
    queryKey: keyCheckKey(key),
    queryFn: () => fetchKeyCheck(key),
    enabled: key !== '',
  });

/** The project's import jobs, refetched whenever a job reports progress (the state changes with it). */
export function useImportJobs(projectId: string) {
  const queryClient = useQueryClient();
  useEffect(
    () =>
      registerInvalidation('importProgress', () => {
        void queryClient.invalidateQueries({ queryKey: importJobsKey(projectId) });
        void queryClient.invalidateQueries({ queryKey: jiraProjectsKey(projectId) });
      }),
    [queryClient, projectId],
  );
  return useQuery({
    queryKey: importJobsKey(projectId),
    queryFn: () => fetchImportJobs(projectId),
  });
}

export function useSaveConnection(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (put: JiraConnectionPut) => saveConnection(projectId, put),
    // The variables carry the write-only token: keep them out of the mutation cache.
    gcTime: 0,
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: importConnectionKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: jiraProjectsKey(projectId) }),
      ]),
  });
}

export const useTestConnection = (projectId: string) =>
  useMutation({ mutationFn: () => testConnection(projectId) });

/** The mapping the server suggests for a Jira project (a re-run keeps the previous job's choices). Read once per visit. */
export const useSuggestedMapping = (
  projectId: string,
  connectionId: string | null,
  jiraProjectKey: string | null,
  previousJobId: string | null,
  enabled: boolean,
) =>
  useQuery({
    queryKey: importMappingKey(projectId, jiraProjectKey ?? '', previousJobId),
    queryFn: () =>
      fetchMapping({ connectionId, jiraProjectKey: jiraProjectKey ?? '', previousJobId }),
    enabled: enabled && jiraProjectKey !== null && connectionId !== null,
    staleTime: Infinity,
    gcTime: 0,
  });
