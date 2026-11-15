import { expect, test } from '@playwright/experimental-ct-react';
import type { TimelineEdgeDto } from '@shiplean/core/api/views';
import type { BarBox } from '../BarBox';
import { GanttEdgesLayer } from '../GanttEdgesLayer';
import { DEFAULT_TIMELINE_OPTIONS, type TimelineOptions } from '../TimelineOptions';
import { timelineTestIds as ids } from '../testIds';
import { FINISHED_TIMELINE } from './stubFeatureTimeline';

// One row per fixture bar; the layout is hand-made, 4 px per hour.
const keys = FINISHED_TIMELINE.bars.map((b) => b.key);
const boxes: BarBox[] = keys.map((key, i) => ({
  key,
  x0: 20 + (i % 5) * 60,
  x1: 70 + (i % 5) * 60,
  y: 10 + i * 26,
  height: 14,
}));
// Edges between rows this hand-made layout has no box for (the wave edge SHIP-185 -> SHIP-186) are skipped.
const edges = FINISHED_TIMELINE.edges.filter((e) => keys.includes(e.from) && keys.includes(e.to));
const criticalPath = FINISHED_TIMELINE.criticalPath;

const layer = (
  over: {
    options?: Partial<TimelineOptions>;
    finished?: boolean;
    edges?: TimelineEdgeDto[];
  } = {},
) => (
  <svg width={1800} height={400}>
    <GanttEdgesLayer
      boxes={boxes}
      edges={over.edges ?? edges}
      criticalPath={criticalPath}
      options={{ ...DEFAULT_TIMELINE_OPTIONS, ...over.options }}
      finished={over.finished ?? true}
      pxPerHour={4}
      originX={0}
    />
  </svg>
);

test('GanttEdgesLayer_WhenDepsOff_ShouldDrawNothing', async ({ mount, page }) => {
  await mount(layer({ options: { deps: false } }));
  await expect(page.getByTestId(ids.edges)).toHaveAttribute('data-boxes', String(boxes.length));
  await expect(page.getByTestId(ids.edge)).toHaveCount(0);
  await expect(page.getByTestId(ids.edgeCritical)).toHaveCount(0);
});

test('GanttEdgesLayer_WhenBlocked_ShouldBeDashed', async ({ mount, page }) => {
  await mount(layer());
  const blocked = page.getByTestId(ids.edge).and(page.locator('[data-kind=blocked]'));
  await expect(blocked).toHaveCount(1);
  await expect(blocked).toHaveAttribute('data-from', 'SHIP-199');
  await expect(blocked.locator('path').first()).toHaveAttribute('stroke-dasharray', '4 3');
  const depends = page.locator('[data-kind=depends]').first();
  await expect(depends.locator('path').first()).not.toHaveAttribute('stroke-dasharray', /./);
});

test('GanttEdgesLayer_WhenFinishedAndCp_ShouldOutlineCriticalPath', async ({ mount, page }) => {
  await mount(layer());
  await expect(page.getByTestId(ids.edgeCritical)).toHaveCount(criticalPath.length);
  await expect(page.locator('[data-testid=timeline-edge][data-critical=true]')).toHaveCount(
    edges.filter((e) => e.onCriticalPath).length,
  );
});

test('GanttEdgesLayer_WhenCpOff_ShouldNotOutline', async ({ mount, page }) => {
  await mount(layer({ options: { cp: false } }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  await expect(page.getByTestId(ids.edgeCritical)).toHaveCount(0);
  await expect(page.locator('[data-testid=timeline-edge][data-critical=true]')).toHaveCount(0);
});

test('GanttEdgesLayer_WhenNotFinished_ShouldNotOutline', async ({ mount, page }) => {
  await mount(layer({ finished: false }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  await expect(page.getByTestId(ids.edgeCritical)).toHaveCount(0);
});

test('GanttEdgesLayer_ShouldShowWaitLabels', async ({ mount, page }) => {
  await mount(layer());
  const labelled = edges.filter((e) => e.waitLabel !== null);
  await expect(page.getByTestId(ids.edgeWaitLabel)).toHaveCount(labelled.length);
  await expect(page.getByTestId(ids.edgeWaitLabel).first()).toHaveText('22.8 h');
  await expect(page.locator('[data-from=SHIP-191][data-to=SHIP-192] title')).toHaveText(
    'SHIP-191 → SHIP-192, waited 22.8 h',
  );
});
