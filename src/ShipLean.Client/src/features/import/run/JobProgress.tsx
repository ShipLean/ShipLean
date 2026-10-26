import type { ImportJobDto } from '../../../api/import';
import type { ProjectDto } from '../../../api/projects';
import { importTestIds as ids } from '../testIds';
import { JobChip } from './JobChip';
import { ProgressCounts } from './ProgressCounts';
import { isFinished, useStopImport } from './runApi';
import { useRerunJob } from './useRerunJob';

// The job's headline: state chip, percent, per-entity counts, and Stop while it runs or Re-run once it has finished.
export function JobProgress({
  project,
  job,
  onJob,
}: {
  project: ProjectDto;
  job: ImportJobDto;
  onJob: (jobId: string) => void;
}) {
  const stop = useStopImport(project.id);
  const rerun = useRerunJob(project, onJob);
  const finished = isFinished(job);
  const failure = stop.error ?? rerun.error;
  return (
    <section
      data-testid={ids.runProgress}
      className="panel flex flex-col gap-[var(--space-3)] p-[var(--space-4)]"
    >
      <header className="flex items-center gap-[var(--space-3)]">
        <h2 className="font-semibold">{job.progress.rerun ? 'Re-run' : 'Progress'}</h2>
        <span className="font-mono text-ink-muted">
          {job.jiraProjectKey} → {job.projectKey}
        </span>
        <span className="ml-auto">
          <JobChip status={job.status} />
        </span>
      </header>
      <p data-testid={ids.runPercent} className="text-[length:var(--text-lg)] font-semibold">
        {job.progress.percent}%
      </p>
      <progress
        max={100}
        value={job.progress.percent}
        aria-label="Import progress"
        className="w-full"
      />
      {job.progress.message === '' ? null : (
        <p
          data-testid={ids.runMessage}
          role={job.status === 'failed' ? 'alert' : undefined}
          className={job.status === 'failed' ? 'text-alert' : 'text-ink-muted'}
        >
          {job.progress.message}
        </p>
      )}
      <ProgressCounts counts={job.progress.counts} />
      {failure ? (
        <p data-testid={ids.runStopError} role="alert" className="text-alert">
          {failure instanceof Error ? failure.message : 'That did not work.'}
        </p>
      ) : null}
      <div>
        {finished ? (
          <button
            type="button"
            data-testid={ids.runRerun}
            className="btn-ghost"
            disabled={rerun.pending}
            onClick={() => rerun.start(job)}
          >
            Re-run
          </button>
        ) : (
          <button
            type="button"
            data-testid={ids.runStop}
            className="btn-ghost"
            disabled={stop.isPending || stop.isSuccess}
            onClick={() => stop.mutate(job.id)}
          >
            {stop.isSuccess ? 'Stopping after this batch…' : 'Stop'}
          </button>
        )}
      </div>
    </section>
  );
}
