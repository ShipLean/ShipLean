import { describe, expect, it } from 'vitest';
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from '../ct/stubFeatureTimeline';
import { DAY_WIDTH, timelineGeometry } from './timelineGeometry';
import { timelineRows } from './timelineRows';

const geometry = (view: FeatureTimelineViewDto, zoom: 'fit' | 'days', width = 816) =>
  timelineGeometry(view, timelineRows(view, 'stories'), zoom, width);

describe('timelineGeometry', () => {
  it('Fit_ShouldScaleTheWholeRangeToTheWidth', () => {
    const g = geometry(FINISHED_TIMELINE, 'fit', 816);
    expect(g.pxPerHour).toBeCloseTo(816 / FINISHED_TIMELINE.scale.endHours);
    expect(g.width).toBeCloseTo(816);
  });

  it('Days_ShouldGiveEachDayTheFixedWidth', () => {
    const g = geometry(FINISHED_TIMELINE, 'days', 816);
    expect(g.pxPerHour).toBe(DAY_WIDTH / 24);
    expect(g.days[0].width).toBe(DAY_WIDTH);
    expect(g.width).toBeCloseTo((FINISHED_TIMELINE.scale.endHours / 24) * DAY_WIDTH);
  });

  it('InFlightBar_ShouldRunToNow', () => {
    const view = RUNNING_TIMELINE;
    const flying = view.bars.find((b) => b.state === 'inFlight' && b.startHours !== null);
    expect(flying).toBeDefined();
    const g = geometry(view, 'fit');
    const mark = g.marks.find((m) => m.key === flying!.key)!;
    expect(mark.box.x1).toBeCloseTo(view.scale.nowHours! * g.pxPerHour);
    expect(g.nowX).toBeCloseTo(view.scale.nowHours! * g.pxPerHour);
  });

  it('ReviewDots_ShouldSitAfterTheBarEndWithADashedLine', () => {
    const g = geometry(FINISHED_TIMELINE, 'fit');
    const mark = g.marks.find((m) => m.key === 'SHIP-191')!;
    expect(mark.reviews.length).toBeGreaterThan(0);
    for (const dot of mark.reviews) expect(dot.x).toBeGreaterThan(mark.box.x1);
    expect(mark.reviewLine!.x0).toBe(mark.box.x1);
    expect(mark.reviewLine!.x1).toBe(Math.max(...mark.reviews.map((d) => d.x)));
  });

  it('Weekends_ShouldComeFromTheDays', () => {
    const g = geometry(FINISHED_TIMELINE, 'fit');
    expect(g.days.map((d) => d.weekend)).toEqual(
      FINISHED_TIMELINE.scale.days.map((d) => d.weekend),
    );
    expect(g.days.some((d) => d.weekend)).toBe(true);
  });

  it('ShortDay_ShouldBeNarrowerInFit', () => {
    const view = structuredClone(FINISHED_TIMELINE);
    const d = view.scale.days;
    // Make the second day 23 hours long: every later day starts an hour earlier.
    for (let i = 2; i < d.length; i++) d[i].startHours -= 1;
    view.scale.endHours -= 1;
    const g = geometry(view, 'fit');
    expect(g.days[1].width).toBeLessThan(g.days[0].width);
  });

  it('InferredBar_ShouldBeADiamondAtItsEnd', () => {
    const g = geometry(FINISHED_TIMELINE, 'fit');
    const mark = g.marks.find((m) => m.key === 'SHIP-198')!;
    expect(mark.kind).toBe('diamond');
    expect(mark.box.x0).toBe(mark.box.x1);
    expect(mark.box.x0).toBeCloseTo(281.5 * g.pxPerHour);
  });

  it('Boxes_ShouldHoldABoxPerMark', () => {
    const g = geometry(FINISHED_TIMELINE, 'fit');
    expect(g.boxes).toHaveLength(FINISHED_TIMELINE.bars.length + FINISHED_TIMELINE.stories.length);
  });
});
