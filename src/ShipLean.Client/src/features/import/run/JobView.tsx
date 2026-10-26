import type { ProjectDto } from '../../../api/projects';
import { ApiRequestError } from '../../../api/client';
import { importTestIds as ids } from '../testIds';
import { JobErrors } from './JobErrors';
import { JobOutcome } from './JobOutcome';
import { JobLog } from './JobLog';
import { JobProgress } from './JobProgress';
import { useImportJob } from './runApi';

// One import job: progress, the live log and the errors to retry.
export function JobView({
  project,
  jobId,
  onJob,
}: {
  project: ProjectDto;
  jobId: string;
  onJob: (jobId: string) => void;
}) {
  const job = useImportJob(project.id, jobId);
  if (job.isPending) {
    return (
      <p data-testid={ids.runJobLoading} className="text-ink-muted">
        Loading the import…
      </p>
    );
  }
  if (job.isError) {
    return (
      <p data-testid={ids.runJobError} role="alert" className="text-alert">
        {job.error instanceof ApiRequestError
          ? job.error.message
          : 'The import could not be loaded.'}
      </p>
    );
  }
  return (
    <>
      <JobProgress project={project} job={job.data} onJob={onJob} />
      <JobOutcome job={job.data} />
      <JobErrors projectId={project.id} job={job.data} />
      <JobLog projectId={project.id} job={job.data} />
    </>
  );
}
