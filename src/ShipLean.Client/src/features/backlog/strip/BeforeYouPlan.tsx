import type { BacklogReviewDto } from '../../../api/backlogWrites';
import type { CheckQueue } from './useCheckQueue';

// The strip of backlog checks above the list.
// TODO: the five checks and the queue bar replace this empty strip.
export function BeforeYouPlan({
  review,
  queue,
}: {
  review: BacklogReviewDto | null;
  queue: CheckQueue;
}) {
  void [review, queue];
  return null;
}
