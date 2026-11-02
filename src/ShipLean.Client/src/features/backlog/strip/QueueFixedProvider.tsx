import { useMemo, type ReactNode } from 'react';
import type { BacklogReviewDto } from '../../../api/backlogWrites';
import type { BacklogRowDto } from '../../../api/views';
import type { CheckQueue } from './useCheckQueue';
import { QueueFixedContext, type QueueFixed } from './queueFixed';

const IDLE: QueueFixed = { fixed: new Set(), total: 0 };

/**
 * Tells the rows and the strip which of the queue's rows are fixed. `queued` is the queue's rows as the list has
 * them now: a frozen id with no row is left out of both numbers.
 */
export function QueueFixedProvider({
  review,
  queue,
  queued,
  children,
}: {
  review: BacklogReviewDto | null;
  queue: CheckQueue;
  queued: readonly BacklogRowDto[];
  children: ReactNode;
}) {
  const value = useMemo<QueueFixed>(() => {
    if (!queue.active) return IDLE;
    const flagged = new Set(review?.checks.find((c) => c.id === queue.checkId)?.itemIds ?? []);
    return {
      fixed: new Set(queued.filter((row) => !flagged.has(row.id)).map((row) => row.id)),
      total: queued.length,
    };
  }, [review, queue.active, queue.checkId, queued]);
  return <QueueFixedContext.Provider value={value}>{children}</QueueFixedContext.Provider>;
}
