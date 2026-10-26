import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import type { ImportJobDto } from '../../../api/import';
import type { ProjectDto } from '../../../api/projects';
import { routePath } from '../../../routes';
import { fetchConnection, fetchMapping, importConnectionKey } from '../importApi';
import { isNewRow } from '../stepper/stepModel';
import { useRerunImport } from './runApi';

/** The stepper's address when a re-run needs the mapping looked at again (a new status, person or type since the last run). */
export const reviewUrl = (projectKey: string, job: ImportJobDto) =>
  `${routePath('jiraImport', { key: projectKey })}?rerun=${encodeURIComponent(job.id)}&jira=${encodeURIComponent(job.jiraProjectKey)}`;

/**
 * Re-run a finished job. Decision 8: the mapping is read first; with nothing new the job re-runs with its saved mapping,
 * otherwise the stepper opens on Mapping for it and nothing is posted.
 */
export function useRerunJob(
  project: ProjectDto,
  onStarted: (jobId: string) => void,
  onFailed?: (error: unknown) => void,
) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const rerun = useRerunImport(project.id);
  const read = useMutation({
    mutationFn: async (job: ImportJobDto) => {
      const connection = await queryClient.ensureQueryData({
        queryKey: importConnectionKey(project.id),
        queryFn: () => fetchConnection(project.id),
      });
      if (connection === null)
        throw new Error('There is no Jira connection to read the mapping with.');
      const mapping = await fetchMapping({
        connectionId: connection.id,
        jiraProjectKey: job.jiraProjectKey,
        previousJobId: job.id,
      });
      return isNewRow(mapping);
    },
  });
  return {
    start: (job: ImportJobDto) =>
      read.mutate(job, {
        onSuccess: (hasNew) => {
          if (hasNew) navigate(reviewUrl(project.key, job));
          else
            rerun.mutate(
              { jobId: job.id, mapping: null },
              { onSuccess: (j) => onStarted(j.id), onError: onFailed },
            );
        },
        onError: onFailed,
      }),
    pending: read.isPending || rerun.isPending,
    error: read.error ?? rerun.error,
  };
}
