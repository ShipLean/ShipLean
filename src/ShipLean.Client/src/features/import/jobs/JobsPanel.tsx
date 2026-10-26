import { useState } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { ProjectDto } from '../../../api/projects';
import { useImportJobs } from '../importApi';
import { importTestIds as ids } from '../testIds';
import { JobRow } from './JobRow';

// The panel behind ProjectJobs: this project's jobs, newest first, each with Re-run (which opens the new run on step 5).
export function JobsPanel({ project }: { project: ProjectDto }) {
  const jobs = useImportJobs(project.id);
  const [note, setNote] = useState<string | null>(null);
  const running = (jobs.data ?? []).some((j) => j.status === 'queued' || j.status === 'running');
  return (
    <section
      data-testid={ids.runJobs}
      className="panel mx-[var(--space-4)] mb-[var(--space-4)] flex flex-col gap-[var(--space-2)] p-[var(--space-4)]"
    >
      <h2 className="font-semibold">Import jobs</h2>
      {note === null ? null : (
        <p data-testid={ids.runJobsNote} role="alert" className="text-alert">
          {note}
        </p>
      )}
      {jobs.isPending ? <p className="text-ink-muted">Loading the import jobs…</p> : null}
      {jobs.isError ? (
        <p data-testid={ids.runJobsError} role="alert" className="text-alert">
          {jobs.error instanceof ApiRequestError
            ? jobs.error.message
            : 'The import jobs could not be loaded.'}
        </p>
      ) : null}
      {jobs.data?.length === 0 ? (
        <p data-testid={ids.runJobsEmpty} className="text-ink-muted">
          No imports yet.
        </p>
      ) : null}
      {jobs.data?.map((job) => (
        <JobRow
          key={job.id}
          project={project}
          job={job}
          disabled={running}
          onFailed={(error) =>
            setNote(error instanceof Error ? error.message : 'The re-run did not start.')
          }
        />
      ))}
    </section>
  );
}
