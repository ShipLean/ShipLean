import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { api } from '../../../api/client';
import type {
  ImportErrorDto,
  ImportJobDto,
  ImportLogLineDto,
  ImportMappingDto,
  ImportStartRequest,
} from '../../../api/import';
import { registerInvalidation } from '../../../realtime';
import {
  importJobsKey,
  jiraProjectsKey,
  type ImportRerunRequest,
  type ImportRetryRequest,
  type ImportSkipRequest,
} from '../importApi';

const jobUrl = (jobId: string) => `/api/import/jobs/${encodeURIComponent(jobId)}`;

export const importJobKey = (projectId: string, jobId: string) =>
  ['projects', projectId, 'import', 'job', jobId] as const;
export const importJobLogKey = (projectId: string, jobId: string) =>
  [...importJobKey(projectId, jobId), 'log'] as const;
export const importJobErrorsKey = (projectId: string, jobId: string) =>
  [...importJobKey(projectId, jobId), 'errors'] as const;

export const startImport = (request: ImportStartRequest) =>
  api.post<ImportJobDto>('/api/import/jobs', request);
export const rerunImport = (jobId: string, mapping: ImportMappingDto | null) =>
  api.post<ImportJobDto>(`${jobUrl(jobId)}/rerun`, {
    mapping,
  } satisfies ImportRerunRequest);
export const stopImport = (jobId: string) => api.post<ImportJobDto>(`${jobUrl(jobId)}/stop`);
export const retryImportError = (jobId: string, errorId: string | null) =>
  api.post<ImportJobDto>(`${jobUrl(jobId)}/retry`, { errorId } satisfies ImportRetryRequest);
export const skipImportError = (jobId: string, errorId: string | null) =>
  api.post<ImportJobDto>(`${jobUrl(jobId)}/skip`, { errorId } satisfies ImportSkipRequest);

export const isFinished = (job: ImportJobDto) =>
  job.status !== 'queued' && job.status !== 'running';

// Until the run has created the target project the tab cannot hear its events, so the job is polled.
export const POLL_MS = 2000;
export const needsPolling = (job: ImportJobDto | undefined) =>
  job !== undefined && job.projectId === null && !isFinished(job);

/** One job, polled while it has no project yet and after that refetched when its progress events arrive. */
export function useImportJob(projectId: string, jobId: string) {
  const queryClient = useQueryClient();
  useEffect(
    () =>
      registerInvalidation('importProgress', (event) => {
        if (event.jobId === jobId) void invalidateJob(queryClient, projectId, jobId);
      }),
    [queryClient, projectId, jobId],
  );
  return useQuery({
    queryKey: importJobKey(projectId, jobId),
    queryFn: () => api.get<ImportJobDto>(jobUrl(jobId)),
    refetchInterval: (query) => (needsPolling(query.state.data) ? POLL_MS : false),
  });
}

export function useImportJobLog(projectId: string, job: ImportJobDto) {
  return useQuery({
    queryKey: importJobLogKey(projectId, job.id),
    queryFn: () => api.get<ImportLogLineDto[]>(`${jobUrl(job.id)}/log`),
    refetchInterval: needsPolling(job) ? POLL_MS : false,
  });
}

export function useImportJobErrors(projectId: string, job: ImportJobDto) {
  return useQuery({
    queryKey: importJobErrorsKey(projectId, job.id),
    queryFn: () => api.get<ImportErrorDto[]>(`${jobUrl(job.id)}/errors`),
    refetchInterval: needsPolling(job) ? POLL_MS : false,
  });
}

async function invalidateJob(queryClient: QueryClient, projectId: string, jobId: string) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: importJobKey(projectId, jobId) }),
    queryClient.invalidateQueries({ queryKey: importJobsKey(projectId) }),
    queryClient.invalidateQueries({ queryKey: jiraProjectsKey(projectId) }),
  ]);
}

// What a write to a job changes on the page: the job it returns replaces the cached one; the log, the errors and the lists refetch.
function useJobWrite<V>(projectId: string, write: (variables: V) => Promise<ImportJobDto>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: write,
    onSuccess: (job) => {
      queryClient.setQueryData(importJobKey(projectId, job.id), job);
      return invalidateJob(queryClient, projectId, job.id);
    },
  });
}

export const useStartImport = (projectId: string) => useJobWrite(projectId, startImport);
export const useRerunImport = (projectId: string) =>
  useJobWrite(projectId, (v: { jobId: string; mapping: ImportMappingDto | null }) =>
    rerunImport(v.jobId, v.mapping),
  );
export const useStopImport = (projectId: string) => useJobWrite(projectId, stopImport);
export const useRetryImportError = (projectId: string, jobId: string) =>
  useJobWrite(projectId, (errorId: string | null) => retryImportError(jobId, errorId));
export const useSkipImportError = (projectId: string, jobId: string) =>
  useJobWrite(projectId, (errorId: string | null) => skipImportError(jobId, errorId));
