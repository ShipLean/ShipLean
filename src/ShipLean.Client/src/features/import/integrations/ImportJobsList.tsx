import { useImportJobs } from '../importApi';
import { importTestIds as ids } from '../testIds';
import { ImportJobRow } from './ImportJobRow';

export function ImportJobsList({ projectId }: { projectId: string }) {
  const jobs = useImportJobs(projectId);
  if (jobs.isPending) return <p className="text-ink-muted">Loading the import jobs…</p>;
  if (jobs.isError) {
    return (
      <p role="alert" className="text-alert">
        The import jobs could not be loaded.
      </p>
    );
  }
  if (jobs.data.length === 0) {
    return (
      <p data-testid={ids.jobsEmpty} className="text-ink-muted">
        No imports yet.
      </p>
    );
  }
  return (
    <ul data-testid={ids.jobsList} className="m-0 list-none p-0">
      {jobs.data.map((job) => (
        <ImportJobRow key={job.id} job={job} />
      ))}
    </ul>
  );
}
