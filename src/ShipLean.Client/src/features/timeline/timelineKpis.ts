import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';

/** The four header figures, numbers only (the component writes the words). A null figure is not known yet. */
export interface TimelineKpiFigures {
  /** Whole days from Added to Finished, or to now while running. */
  leadDays: number | null;
  /** Calendar days of execution, so far while running. */
  executionDays: number | null;
  tasksStarted: number;
  tasksTotal: number;
  notStarted: number;
  noStart: number;
  reviews: number;
  changesRequested: number;
}

export function timelineKpis(view: FeatureTimelineViewDto): TimelineKpiFigures {
  const gapDays = view.gaps.flatMap((g) => (g.days === null ? [] : [g.days]));
  const reviews = view.bars.flatMap((b) => b.reviews);
  const notStarted = view.notStarted.filter((n) => n.type === 'task').length;
  return {
    leadDays: gapDays.length === 0 ? null : Math.round(gapDays.reduce((a, b) => a + b, 0)),
    executionDays: view.execution.calendarDays,
    tasksStarted: view.bars.filter((b) => !b.inferred).length,
    tasksTotal: view.bars.length + notStarted,
    notStarted,
    noStart: view.bars.filter((b) => b.inferred).length,
    reviews: reviews.length,
    changesRequested: reviews.filter((r) => r.verdict === 'changesRequested').length,
  };
}
