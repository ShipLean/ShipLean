import { Link, useNavigate } from 'react-router-dom';
import type { ImportJobDto } from '../../../api/import';
import type { ProjectDto } from '../../../api/projects';
import { routePath } from '../../../routes';
import { jobStates } from '../run/jobStates';
import { useRerunJob } from '../run/useRerunJob';
import { importTestIds as ids } from '../testIds';

export function JobRow({
  project,
  job,
  disabled,
  onFailed,
}: {
  project: ProjectDto;
  job: ImportJobDto;
  // A job of this project is running: nothing else can start.
  disabled: boolean;
  onFailed: (error: unknown) => void;
}) {
  const navigate = useNavigate();
  const open = (jobId: string) =>
    `${routePath('jiraImport', { key: project.key })}?job=${encodeURIComponent(jobId)}`;
  const rerun = useRerunJob(project, (id) => navigate(open(id)), onFailed);
  return (
    <div
      data-testid={ids.runJobsRow}
      data-status={job.status}
      className="flex items-center gap-[var(--space-3)] border-t border-edge py-2 text-[length:var(--text-sm)]"
    >
      <span className="font-mono">
        {job.jiraProjectKey} → {job.projectKey}
      </span>
      <span data-testid={ids.runJobsStatus} className="text-ink-muted">
        {jobStates[job.status].label}
      </span>
      <span className="text-ink-muted">
        {job.finishedLabel ?? job.startedLabel ?? 'Not started'}
      </span>
      <Link data-testid={ids.runJobsOpen} className="ml-auto" to={open(job.id)}>
        Open
      </Link>
      <button
        type="button"
        data-testid={ids.runJobsRerun}
        className="btn-ghost"
        disabled={disabled || rerun.pending}
        onClick={() => rerun.start(job)}
      >
        Re-run
      </button>
    </div>
  );
}
