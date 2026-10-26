import { ApiRequestError } from '../../../api/client';
import type { ImportJobDto } from '../../../api/import';
import { importTestIds as ids } from '../testIds';
import { ErrorRow } from './ErrorRow';
import { isFinished, useImportJobErrors, useRetryImportError, useSkipImportError } from './runApi';

// The rows that failed. Retry is synchronous (the server pages Jira until the issue turns up), so the row shows a busy
// state until the answer comes; the server refuses with a 409 while the job still runs.
export function JobErrors({ projectId, job }: { projectId: string; job: ImportJobDto }) {
  const errors = useImportJobErrors(projectId, job);
  const retry = useRetryImportError(projectId, job.id);
  const skip = useSkipImportError(projectId, job.id);
  const rows = errors.data ?? [];
  const pending = rows.filter((e) => e.resolution === 'pending');
  if (rows.length === 0) return null;

  const inFlight = retry.isPending || skip.isPending;
  const busyId = retry.isPending ? retry.variables : skip.isPending ? skip.variables : undefined;
  const failure = retry.error ?? skip.error;
  const locked = inFlight || !isFinished(job);
  return (
    <section
      data-testid={ids.runErrors}
      className="panel flex flex-col gap-[var(--space-2)] p-[var(--space-4)]"
    >
      <header className="flex items-center gap-[var(--space-3)]">
        <h2 className="font-semibold">Errors</h2>
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          {pending.length} of {rows.length} still to resolve. Everything else imported.
        </span>
        {pending.length > 0 ? (
          <button
            type="button"
            data-testid={ids.runRetryAll}
            className="btn-ghost ml-auto"
            disabled={locked}
            onClick={() => retry.mutate(null)}
          >
            {retry.isPending && busyId === null ? 'Retrying…' : 'Retry all'}
          </button>
        ) : null}
      </header>
      {failure ? (
        <p data-testid={ids.runErrorsNote} role="alert" className="text-alert">
          {failure instanceof ApiRequestError ? failure.message : 'That did not work.'}
        </p>
      ) : null}
      <table className="w-full text-left text-[length:var(--text-sm)]">
        <thead className="text-ink-muted">
          <tr>
            <th>Jira issue</th>
            <th>What</th>
            <th>Error</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {rows.map((error) => (
            <ErrorRow
              key={error.id}
              error={error}
              busy={inFlight && busyId === error.id}
              locked={locked}
              onRetry={() => retry.mutate(error.id)}
              onSkip={() => skip.mutate(error.id)}
            />
          ))}
        </tbody>
      </table>
    </section>
  );
}
