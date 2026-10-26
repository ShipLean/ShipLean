import type { ImportErrorDto } from '../../../api/import';
import { importTestIds as ids } from '../testIds';

// One row that did not import. A retry that fails again leaves a new pending row, so Skip is offered on any pending row.
export function ErrorRow({
  error,
  busy,
  locked,
  onRetry,
  onSkip,
}: {
  error: ImportErrorDto;
  // This row's retry or skip is in flight.
  busy: boolean;
  // Something else is in flight, or the job is still running.
  locked: boolean;
  onRetry: () => void;
  onSkip: () => void;
}) {
  const pending = error.resolution === 'pending';
  return (
    <tr
      data-testid={ids.runErrorRow}
      data-resolution={error.resolution}
      className="border-t border-edge"
    >
      <td className="py-2 font-mono">{error.sourceKey}</td>
      <td className="py-2">{error.entity}</td>
      <td className="py-2">{error.message}</td>
      <td className="py-2 text-right">
        {busy ? (
          <span data-testid={ids.runErrorBusy} role="status" className="text-ink-muted">
            Working…
          </span>
        ) : pending ? (
          <span className="flex justify-end gap-[var(--space-2)]">
            <button
              type="button"
              data-testid={ids.runErrorSkip}
              className="btn-ghost"
              disabled={locked}
              onClick={onSkip}
            >
              Skip
            </button>
            <button
              type="button"
              data-testid={ids.runErrorRetry}
              className="btn-ghost"
              disabled={locked}
              onClick={onRetry}
            >
              Retry
            </button>
          </span>
        ) : (
          <span className="text-ink-muted">
            {error.resolution === 'retried' ? 'Retried' : 'Skipped'}
          </span>
        )}
      </td>
    </tr>
  );
}
