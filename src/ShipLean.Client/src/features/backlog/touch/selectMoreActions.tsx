import { useMemo } from 'react';
import type { BacklogReviewDto } from '@shiplean/core/api/backlogWrites';
import type { SheetAction } from '../../../touch/ActionSheet';
import type { CheckQueue } from '../strip/useCheckQueue';
import { estimateIcon, selectIcon } from '../toolbar/moreIcons';
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
  const unestimated = useMemo(
    () => review?.checks.find((check) => check.id === 'unestimated')?.itemIds ?? [],
    [review],
  );
  const count = unestimated.length;
  return useMemo(
    () => [
      {
        id: 'select',
        label: 'Select',
        icon: selectIcon,
        hint: 'Then change state, points or sprint for many at once',
        onSelect: start,
        disabled: selecting,
      },
      ...(count === 0
        ? []
        : [
            {
              id: 'estimate-unpointed',
              label: 'Estimate what has no points',
              icon: estimateIcon,
              detail: count,
              onSelect: () => queue.open('unestimated', unestimated),
            },
          ]),
    ],
    [selecting, start, queue, count, unestimated],
  );
}
