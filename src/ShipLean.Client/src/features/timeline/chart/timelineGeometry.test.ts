import { describe, expect, it } from 'vitest';
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from '../ct/stubFeatureTimeline';
import { durationLabelBox } from './labelBoxes';
import { DAY_WIDTH, ROW_HEIGHT, timelineGeometry } from './timelineGeometry';
import { timelineRows } from './timelineRows';

const geometry = (view: FeatureTimelineViewDto, zoom: 'fit' | 'days', width = 816) =>
  timelineGeometry(view, timelineRows(view, 'stories'), zoom, width);

describe('timelineGeometry', () => {
  it('Fit_ShouldScaleTheWholeRangeToTheWidth', () => {
    const g = geometry(FINISHED_TIMELINE, 'fit', 816);
    expect(g.pxPerHour).toBeCloseTo(816 / FINISHED_TIMELINE.scale.endHours);
    expect(g.width).toBeGreaterThanOrEqual(816);
  });

  it.each(['fit', 'days'] as const)('%s_ShouldFitEveryDurationLabelInsideTheWidth', (zoom) => {
    const g = geometry(FINISHED_TIMELINE, zoom, 816);
    const ends = g.marks.flatMap((m) => {
      const box = m.durationLabel === null ? null : durationLabelBox(m, m.durationLabel);
      return box === null ? [] : [box.x1];
    });
    expect(ends.length).toBeGreaterThan(0);
    expect(Math.max(...ends)).toBeLessThanOrEqual(g.width);
  });

  it('Days_ShouldGiveEachDayTheFixedWidth', () => {
    const g = geometry(FINISHED_TIMELINE, 'days', 816);
    expect(g.pxPerHour).toBe(DAY_WIDTH / 24);
    expect(g.days[0].width).toBe(DAY_WIDTH);
    expect(g.width).toBeGreaterThanOrEqual((FINISHED_TIMELINE.scale.endHours / 24) * DAY_WIDTH);
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

  it('NotStartedRow_ShouldPushTheNextMarkDownOneRowAndGetNoMark', () => {
    const [task] = RUNNING_TIMELINE.notStarted.filter((i) => i.parent !== null);
    const without: FeatureTimelineViewDto = { ...RUNNING_TIMELINE, notStarted: [] };
    const withRow: FeatureTimelineViewDto = {
      ...RUNNING_TIMELINE,
      notStarted: [{ ...task, key: 'SHIP-130', parent: 'SHIP-120' }],
    };
    const before = geometry(without, 'fit');
    const after = geometry(withRow, 'fit');
    const y = (g: typeof before, key: string) => g.marks.find((m) => m.key === key)!;

    expect(after.height).toBe(before.height + ROW_HEIGHT);
    expect(after.marks).toHaveLength(before.marks.length);
    expect(after.marks.some((m) => m.key === 'SHIP-130')).toBe(false);
    // SHIP-120's own rows stay put; the next story and its bars move down one row.
    expect(y(after, 'SHIP-122').box.y).toBe(y(before, 'SHIP-122').box.y);
    expect(y(after, 'SHIP-141').box.y).toBe(y(before, 'SHIP-141').box.y + ROW_HEIGHT);
    expect(y(after, 'SHIP-171').box.y).toBe(y(before, 'SHIP-171').box.y + ROW_HEIGHT);
    expect(y(after, 'SHIP-171').row).toBe(y(before, 'SHIP-171').row + 1);
    // The edges layer reads the same boxes, so arrows land on the moved rows too.
    expect(after.boxes.find((b) => b.key === 'SHIP-171')!.y).toBe(y(after, 'SHIP-171').box.y);
  });

  it('Marks_ShouldCarryTheServerDurationLabel', () => {
    const g = geometry(FINISHED_TIMELINE, 'fit');
    const bar = FINISHED_TIMELINE.bars.find((b) => b.key === 'SHIP-191')!;
    const story = FINISHED_TIMELINE.stories.find((s) => s.key === 'SHIP-185')!;
    expect(g.marks.find((m) => m.key === 'SHIP-191')!.durationLabel).toBe(bar.durationLabel);
    expect(g.marks.find((m) => m.key === 'SHIP-185')!.durationLabel).toBe(story.durationLabel);
    expect(g.marks.find((m) => m.key === 'SHIP-198')!.durationLabel).toBeNull();
  });

  it('AxisLabels_WhenFit_ShouldLabelEachWeekStartWithItsShortLabel', () => {
    const g = geometry(FINISHED_TIMELINE, 'fit', 816);
    const weeks = FINISHED_TIMELINE.scale.days.filter((d) => d.weekStart).map((d) => d.shortLabel);
    expect(weeks.length).toBeGreaterThan(1);
    expect(g.axisLabels.map((l) => l.text)).toEqual(weeks);
    expect(g.axisLabels.every((l) => l.kind === 'week')).toBe(true);
  });

  it('AxisLabels_WhenFitAndNoMonday_ShouldLabelTheFirstDay', () => {
    const view = {
      ...FINISHED_TIMELINE,
      scale: {
        ...FINISHED_TIMELINE.scale,
        days: FINISHED_TIMELINE.scale.days.map((d) => ({ ...d, weekStart: false })),
      },
    };
    const g = geometry(view, 'fit', 816);
    expect(g.axisLabels.map((l) => l.text)).toEqual([FINISHED_TIMELINE.scale.days[0].shortLabel]);
  });

  it('AxisLabels_WhenZoomIsDays_ShouldGiveOneLabelPerDay', () => {
    const g = geometry(FINISHED_TIMELINE, 'days', 300);
    expect(g.axisLabels.map((l) => l.text)).toEqual(
      FINISHED_TIMELINE.scale.days.map((d) => d.label),
    );
  });
});
