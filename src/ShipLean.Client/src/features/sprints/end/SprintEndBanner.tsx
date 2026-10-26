import type { RolloverDto } from '../../../api/sprints';
import { sprintsTestIds as ids } from '../testIds';

/** What Decide later leaves behind: the unfinished count, and Review to reopen the dialog. */
export function SprintEndBanner({
  rollover,
  onReview,
}: {
  rollover: RolloverDto;
  onReview: () => void;
}) {
  const count = rollover.items.length;
  return (
    <div
      data-testid={ids.endBanner}
      role="status"
      style={{ background: 'color-mix(in srgb, var(--color-warn) 16%, var(--color-panel))' }}
      className="flex items-center gap-2 border-b border-edge px-4 py-2 text-[length:var(--text-sm)]"
    >
      <span>
        <b>{`${rollover.sprint.name} has ${count} unfinished ${count === 1 ? 'item' : 'items'}.`}</b>{' '}
        Decide where they go.
      </span>
      <button type="button" data-testid={ids.endReview} className="btn-ghost" onClick={onReview}>
        Review
      </button>
    </div>
  );
}
