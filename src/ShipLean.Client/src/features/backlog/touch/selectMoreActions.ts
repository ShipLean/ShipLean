import { useMemo } from 'react';
import type { BacklogReviewDto } from '@shiplean/core/api/backlogWrites';
import type { SheetAction } from '../../../touch/ActionSheet';
import type { CheckQueue } from '../strip/useCheckQueue';
import { useTouchSelectMode } from './selectMode';

/**
 * The two rows ⋯ More gains at touch for a mode that has a selection (List, Planning): Select, and Estimate what has no
 * points, which opens the not-estimated queue and is left out while no row is unestimated.
 */
export function useSelectMoreActions(
  queue: CheckQueue,
  review: BacklogReviewDto | null,
): SheetAction[] {
  const { selecting, start } = useTouchSelectMode();
  const unestimated = review?.checks.find((check) => check.id === 'unestimated')?.itemIds ?? [];
  const count = unestimated.length;
  return useMemo(
    () => [
      { id: 'select', label: 'Select', onSelect: start, disabled: selecting },
      ...(count === 0
        ? []
        : [
            {
              id: 'estimate-unpointed',
              label: 'Estimate what has no points',
              onSelect: () => queue.open('unestimated', unestimated),
            },
          ]),
    ],
    // `unestimated` is a fresh array only when the review is.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [selecting, start, queue, count, review],
  );
}
