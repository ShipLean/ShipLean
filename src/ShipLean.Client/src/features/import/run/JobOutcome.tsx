import { Link } from 'react-router-dom';
import type { ImportJobDto } from '../../../api/import';
import { routePath } from '../../../routes';
import { importTestIds as ids } from '../testIds';

const n = (value: number) => value.toLocaleString('en-GB');

// What a job that ran to the end came to, with the way into the backlog it filled. A re-run says what it touched.
export function JobOutcome({ job }: { job: ImportJobDto }) {
  if (job.status !== 'done' && job.status !== 'doneWithErrors') return null;
  const { counts, rerun, rerunResult } = job.progress;
  const failed = Object.values(counts).reduce((sum, c) => sum + c.failed, 0);
  const withErrors = job.status === 'doneWithErrors';
  return (
    <section
      data-testid={ids.runOutcome}
      data-outcome={withErrors ? 'errors' : 'ok'}
      className="panel flex items-center gap-[var(--space-3)] p-[var(--space-4)]"
    >
      <div className="flex flex-1 flex-col">
        <b>
          {withErrors
            ? `Finished with ${n(failed)} ${failed === 1 ? 'error' : 'errors'}`
            : rerun
              ? 'Re-run finished'
              : 'Import finished'}
        </b>
        <span className="text-ink-muted">
          {n(counts.issues.done)} of {n(counts.issues.total)} issues imported into {job.projectKey}.
          {withErrors
            ? ' Retry the errors below, or re-run later: only what is missing or changed is touched.'
            : ''}
        </span>
        {rerun && rerunResult !== null ? (
          <span data-testid={ids.runRerunResult}>
            {n(rerunResult.updated)} updated · {n(rerunResult.unchanged)} unchanged ·{' '}
            {n(rerunResult.added)} added
          </span>
        ) : null}
      </div>
      <Link className="btn-signal" to={routePath('storiesBacklog', { key: job.projectKey })}>
        Open {job.projectKey} backlog
      </Link>
    </section>
  );
}
