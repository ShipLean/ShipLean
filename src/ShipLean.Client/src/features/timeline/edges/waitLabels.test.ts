import { describe, expect, it } from 'vitest';
import type { FeatureTimelineViewDto, TimelineEdgeDto } from '@shiplean/core/api/views';
import { FINISHED_TIMELINE } from '../ct/stubFeatureTimeline';
import { durationLabelBox, markBox, type LabelBox } from '../chart/labelBoxes';
import { timelineGeometry, type Mark } from '../chart/timelineGeometry';
import { timelineRows } from '../chart/timelineRows';
import { TIMELINE_GROUPS } from '../TimelineOptions';
import { edgePaths } from './edgePaths';
import { placeWaitLabels } from './waitLabels';

const overlaps = (a: LabelBox, b: LabelBox) =>
  a.x0 < b.x1 && b.x0 < a.x1 && a.y0 < b.y1 && b.y0 < a.y1;

const place = (
  view: FeatureTimelineViewDto,
  group: (typeof TIMELINE_GROUPS)[number],
  zoom: 'fit' | 'days',
  width: number,
) => {
  const { marks, pxPerHour } = timelineGeometry(view, timelineRows(view, group), zoom, width);
  const paths = edgePaths(marks, view.edges, { pxPerHour, originX: 0 });
  return { marks, paths, labels: placeWaitLabels(view.edges, paths, marks) };
};

const layouts = TIMELINE_GROUPS.flatMap((group) =>
  [
    ['fit', 600],
    ['fit', 960],
    ['fit', 1800],
    ['days', 960],
  ].map(([zoom, width]) => ({ group, zoom: zoom as 'fit' | 'days', width: width as number })),
);

const mark = (key: string, x0: number, x1: number, row: number): Mark => ({
  key,
  kind: 'bar',
  row,
  box: { key, x0, x1, y: 48 + row * 30, height: 14 },
  reviews: [],
  reviewLine: null,
  durationLabel: null,
});
/** A wide bar from y0 to y1 that blocks every spot it covers; A's arrow into B runs at y = 115. */
const block = (key: string, y0: number, y1: number): Mark => ({
  ...mark(key, 0, 600, 0),
  box: { key, x0: 0, x1: 600, y: y0, height: y1 - y0 },
});
const edge = (over: Partial<TimelineEdgeDto>): TimelineEdgeDto => ({
  from: 'A',
  to: 'B',
  kind: 'depends',
  source: 'link',
  waitHours: 10,
  waitLabel: '10 h',
  unblockedAt: null,
  unblockedHours: null,
  onCriticalPath: false,
  ...over,
});
const scale = { pxPerHour: 1, originX: 0 };

describe('placeWaitLabels', () => {
  it.each(layouts)(
    'PlaceWaitLabels_WhenFinished_ShouldOverlapNoBarDurationOrOtherLabel ($group, $zoom, $width)',
    ({ group, zoom, width }) => {
      const { marks, labels } = place(FINISHED_TIMELINE, group, zoom, width);
      const taken = marks.flatMap((m) => {
        const duration = m.durationLabel === null ? null : durationLabelBox(m, m.durationLabel);
        return duration === null ? [markBox(m)] : [markBox(m), duration];
      });
      for (const label of labels) {
        expect(taken.filter((box) => overlaps(label.box, box))).toEqual([]);
        expect(labels.filter((other) => other !== label && overlaps(label.box, other.box))).toEqual(
          [],
        );
      }
    },
  );

  it('PlaceWaitLabels_WhenFinished_ShouldEndEveryTextInWaitAndPlaceSomeOncePerEdge', () => {
    const { paths, labels } = place(FINISHED_TIMELINE, 'stories', 'fit', 960);
    expect(labels.length).toBeGreaterThan(0);
    for (const label of labels) {
      expect(label.text.endsWith(' wait')).toBe(true);
      const path = paths.find((p) => p.from === label.from && p.to === label.to);
      expect(label.text).toBe(`${path?.waitLabel} wait`);
    }
    const pairs = labels.map((l) => `${l.from}→${l.to}`);
    expect(new Set(pairs).size).toBe(pairs.length);
  });

  it('PlaceWaitLabels_WhenSpaceIsFree_ShouldCentreAboveTheHorizontalSegment', () => {
    const marks = [mark('A', 0, 50, 0), mark('B', 300, 400, 2)];
    const paths = edgePaths(marks, [edge({})], scale);
    const [label] = placeWaitLabels([edge({})], paths, marks);
    expect(label.text).toBe('10 h wait');
    expect(label.x).toBe((58 + 300) / 2);
    expect(label.box.y1).toBeLessThanOrEqual(paths[0].horizontal.y);
  });

  it('PlaceWaitLabels_WhenAboveIsTaken_ShouldGoBelow', () => {
    const marks = [mark('A', 0, 50, 0), mark('B', 300, 400, 2), block('C', 95, 114)];
    const paths = edgePaths(marks, [edge({})], scale);
    const [label] = placeWaitLabels([edge({})], paths, marks);
    expect(label.box.y0).toBeGreaterThanOrEqual(paths[0].horizontal.y);
  });

  it('PlaceWaitLabels_WhenNoSpotIsFree_ShouldDropTheLabel', () => {
    // Wide bars cover every spot above and below both segments.
    const marks = [
      mark('A', 0, 50, 0),
      mark('B', 300, 400, 2),
      block('C', 95, 114),
      block('D', 116, 135),
      block('E', 30, 54),
    ];
    const paths = edgePaths(marks, [edge({})], scale);
    expect(paths).toHaveLength(1);
    expect(placeWaitLabels([edge({})], paths, marks)).toEqual([]);
  });

  it('PlaceWaitLabels_WhenTwoWantOneSpot_ShouldPlaceTheCriticalPathOneFirst', () => {
    // Two arrows from the same spot into a near B: only one label fits below the short segment, and the
    // critical-path arrow wins it even with the shorter wait.
    const marks = [
      mark('A', 0, 50, 0),
      mark('X', 0, 50, 0),
      mark('B', 100, 400, 2),
      block('C', 95, 114),
    ];
    const edges = [
      edge({ from: 'X', waitHours: 90, waitLabel: '90 h' }),
      edge({ waitHours: 5, waitLabel: '5 h', onCriticalPath: true }),
    ];
    const labels = placeWaitLabels(edges, edgePaths(marks, edges, scale), marks);
    expect(labels.map((l) => l.text)).toEqual(['5 h wait']);
  });

  it('PlaceWaitLabels_WhenNotOnThePath_ShouldPlaceTheLongerWaitFirst', () => {
    const marks = [mark('A', 0, 50, 0), mark('X', 0, 50, 4), mark('B', 300, 400, 2)];
    const edges = [
      edge({ waitHours: 5, waitLabel: '5 h' }),
      edge({ from: 'X', waitHours: 90, waitLabel: '90 h' }),
    ];
    const labels = placeWaitLabels(edges, edgePaths(marks, edges, scale), marks);
    expect(labels.map((l) => l.text)).toEqual(['90 h wait', '5 h wait']);
  });

  it('PlaceWaitLabels_WhenTheEdgeHasNoWait_ShouldPlaceNothing', () => {
    const marks = [mark('A', 0, 50, 0), mark('B', 300, 400, 2)];
    const edges = [edge({ waitHours: null, waitLabel: null })];
    expect(placeWaitLabels(edges, edgePaths(marks, edges, scale), marks)).toEqual([]);
  });
});
