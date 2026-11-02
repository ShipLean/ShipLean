import type { CheckQueue } from '../strip/useCheckQueue';
import { backlogTestIds as ids } from '../testIds';
import type { BacklogSearch } from './useBacklogSearch';

// Planning's banner that dragging and the pull line are off while a search or a check queue narrows the rows.
export function MovingOffBanner({ search, queue }: { search: BacklogSearch; queue: CheckQueue }) {
  if (!search.active && !queue.active) return null;
  return (
    <div
      data-testid={ids.dragOffBanner}
      role="status"
      className="flex-none border-b border-edge px-[var(--space-4)] py-1.5 text-[length:var(--text-sm)]"
      style={{ borderLeft: '3px solid var(--color-info)' }}
    >
      Dragging and the pull line are off while a search or check is on, because rank is relative to
      rows you can&apos;t see. Moving to a sprint, the top or the bottom still works.
    </div>
  );
}
