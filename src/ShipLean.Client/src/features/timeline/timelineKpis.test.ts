import { describe, expect, it } from 'vitest';
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './ct/stubFeatureTimeline';
import { timelineKpis } from './timelineKpis';

describe('timelineKpis', () => {
  it('timelineKpis_Finished_ShouldCountLeadExecutionTasksAndReviews', () => {
    expect(timelineKpis(FINISHED_TIMELINE)).toEqual({
      leadDays: 64,
      executionDays: 16,
      tasksStarted: 8,
      tasksTotal: 9,
      notStarted: 0,
      noStart: 1,
      reviews: 9,
      changesRequested: 1,
    });
  });

  it('timelineKpis_Running_ShouldRunLeadToNowAndCountNotStartedTasks', () => {
    expect(timelineKpis(RUNNING_TIMELINE)).toEqual({
      leadDays: 33,
      executionDays: 32.2,
      tasksStarted: 4,
      tasksTotal: 5,
      notStarted: 1,
      noStart: 0,
      reviews: 2,
      changesRequested: 0,
    });
  });

  it('timelineKpis_NoExecution_ShouldGiveNullExecution', () => {
    const view = {
      ...RUNNING_TIMELINE,
      execution: { start: null, end: null, calendarDays: null },
    } as FeatureTimelineViewDto;
    expect(timelineKpis(view).executionDays).toBeNull();
  });

  it('timelineKpis_NoGapDays_ShouldGiveNullLead', () => {
    const view = {
      ...RUNNING_TIMELINE,
      gaps: RUNNING_TIMELINE.gaps.map((g) => ({ ...g, days: null, label: null })),
    } as FeatureTimelineViewDto;
    expect(timelineKpis(view).leadDays).toBeNull();
  });

  it('timelineKpis_NoReviews_ShouldGiveZero', () => {
    const view = {
      ...FINISHED_TIMELINE,
      bars: FINISHED_TIMELINE.bars.map((b) => ({ ...b, reviews: [] })),
    } as FeatureTimelineViewDto;
    const kpis = timelineKpis(view);
    expect(kpis.reviews).toBe(0);
    expect(kpis.changesRequested).toBe(0);
  });
});
