import { describe, expect, it } from 'vitest';
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './ct/stubFeatureTimeline';

// The pinned fixtures every timeline test reads carry the C1 view model's shape: each top-level key, asOfLabel, every day's
// shortLabel and weekStart, and every not-started item's assignee. A Record keyed by the type keeps this list complete.
const VIEW_KEYS: Record<keyof FeatureTimelineViewDto, true> = {
  feature: true,
  asOf: true,
  asOfLabel: true,
  finished: true,
  keyDates: true,
  gaps: true,
  execution: true,
  scale: true,
  criticalPath: true,
  stories: true,
  bars: true,
  edges: true,
  notStarted: true,
  links: true,
};

const FIXTURES: [string, FeatureTimelineViewDto][] = [
  ['finished', FINISHED_TIMELINE],
  ['running', RUNNING_TIMELINE],
];

describe.each(FIXTURES)('the %s timeline fixture', (_name, view) => {
  it('Fixture_WhenRead_ShouldHaveEveryTopLevelKeyOfTheViewModel', () => {
    expect(Object.keys(view).sort()).toEqual(Object.keys(VIEW_KEYS).sort());
  });

  it('Fixture_WhenRead_ShouldHaveAStringAsOfLabel', () => {
    expect(typeof view.asOfLabel).toBe('string');
    expect(view.asOfLabel).not.toBe('');
  });

  it('Fixture_WhenRead_ShouldGiveEveryDayAShortLabelAndWeekStart', () => {
    expect(view.scale.days.length).toBeGreaterThan(0);
    for (const day of view.scale.days) {
      expect(typeof day.shortLabel).toBe('string');
      expect(typeof day.weekStart).toBe('boolean');
    }
  });

  it('Fixture_WhenRead_ShouldGiveEveryNotStartedItemAnAssigneeKey', () => {
    for (const item of view.notStarted) {
      expect(Object.keys(item)).toContain('assignee');
      expect(item.assignee === null || typeof item.assignee === 'object').toBe(true);
    }
  });
});

describe('the running timeline fixture', () => {
  it('Fixture_WhenRunning_ShouldHaveNotStartedItemsWithAndWithoutAnAssignee', () => {
    expect(RUNNING_TIMELINE.notStarted.some((item) => item.assignee === null)).toBe(true);
    expect(
      RUNNING_TIMELINE.notStarted.some((item) => item.assignee !== null && !item.assignee.agent),
    ).toBe(true);
  });
});
