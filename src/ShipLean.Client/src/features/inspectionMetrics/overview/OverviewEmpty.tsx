import { inspectionMetricsTestIds as ids } from '../testIds';

/** The Overview with nothing to headline: no finished sprint yet, or no task ended in the range. */
export function OverviewEmpty({
  unit,
  running,
  noTasks,
}: {
  unit: 'sprint' | 'week';
  running: string | null;
  noTasks: boolean;
}) {
  return (
    <div data-testid={ids.empty} className="panel grid justify-items-start gap-1 p-6">
      {noTasks ? (
        <>
          <h2 className="text-[length:var(--text-lg)] font-semibold">No task has ended yet</h2>
          <p className="m-0 text-ink-muted">
            Lead time is measured per task, from its first move to Active to its last commit. Tasks
            show here once they end.
          </p>
        </>
      ) : (
        <>
          <h2 className="text-[length:var(--text-lg)] font-semibold">No {unit} has finished yet</h2>
          <p className="m-0 text-ink-muted">
            The headline is the last finished {unit}.
            {running === null ? '' : ` ${running} is still running; its tasks so far are below.`}
          </p>
        </>
      )}
    </div>
  );
}
