import type { BacklogCheckId, BacklogReviewDto } from '../../../api/backlogWrites';
import { useListCommand } from '../commands/listCommands';
import { backlogTestIds as ids } from '../testIds';
import type { CheckQueue } from './useCheckQueue';

const LABELS: Record<BacklogCheckId, string> = {
  unestimated: 'not estimated',
  no_acceptance_criteria: 'no acceptance criteria',
  no_parent: 'no parent',
  stale: 'stale',
  likely_duplicate: 'likely duplicates',
};

// While a queue is open Esc leaves it. Mounted only then, so it is the last to register `clear`.
function QueueEscape({ exit }: { exit: () => void }) {
  useListCommand('clear', exit);
  return null;
}

// The strip of the five backlog checks above the list. A count opens the queue of its rows; it never blocks anything.
export function BeforeYouPlan({
  review,
  queue,
}: {
  review: BacklogReviewDto | null;
  queue: CheckQueue;
}) {
  if (!review) return null;
  const open = review.checks.find((check) => check.id === queue.checkId);
  const flagged = new Set(open?.itemIds ?? []);
  const fixed = queue.frozenIds.filter((id) => !flagged.has(id)).length;
  return (
    <div
      data-testid={ids.planStrip}
      className="flex min-h-9 shrink-0 flex-wrap items-center gap-2 border-b border-edge bg-canvas px-4 py-1 text-sm"
    >
      <span className="text-ink-muted">Before you plan</span>
      {review.checks.map((check) => {
        const count = check.itemIds.length;
        const pressed = queue.checkId === check.id;
        return (
          <button
            key={check.id}
            type="button"
            data-testid={ids.planCheck}
            data-check={check.id}
            data-done={count === 0}
            aria-pressed={pressed}
            disabled={count === 0 && !pressed}
            onClick={() => queue.open(check.id, check.itemIds)}
            className={`inline-flex h-6 items-center gap-1.5 whitespace-nowrap rounded-full border px-2 text-xs font-medium ${
              pressed ? 'border-signal bg-signal-soft font-semibold' : 'border-edge bg-panel'
            } ${count === 0 ? 'text-ink-muted' : ''}`}
          >
            <b className="tabular-nums">{count === 0 ? '✓' : count}</b> {LABELS[check.id]}
          </button>
        );
      })}
      {queue.active ? (
        <>
          <QueueEscape exit={queue.exit} />
          <span className="flex-1" />
          <span data-testid={ids.planQueueBar} data-fixed={fixed}>
            {fixed} of {queue.frozenIds.length} fixed
          </span>
          <button
            type="button"
            data-testid={ids.planQueueExit}
            className="btn ghost"
            onClick={queue.exit}
          >
            Leave <kbd>Esc</kbd>
          </button>
        </>
      ) : null}
    </div>
  );
}
