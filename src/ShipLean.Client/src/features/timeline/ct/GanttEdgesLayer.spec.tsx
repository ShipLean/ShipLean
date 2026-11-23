import { expect, test } from '@playwright/experimental-ct-react';
import type { TimelineEdgeDto } from '@shiplean/core/api/views';
import { GanttEdgesLayer } from '../GanttEdgesLayer';
import { DEFAULT_TIMELINE_OPTIONS, type TimelineOptions } from '../TimelineOptions';
import { markBox } from '../chart/labelBoxes';
import { timelineGeometry } from '../chart/timelineGeometry';
import { timelineRows } from '../chart/timelineRows';
import { timelineTestIds as ids } from '../testIds';
import { FINISHED_TIMELINE } from './stubFeatureTimeline';

// The finished fixture's real geometry (Stories, Fit) at the 1800px width the layer is mounted at.
const WIDTH = 1800;
const geometry = timelineGeometry(
  FINISHED_TIMELINE,
  timelineRows(FINISHED_TIMELINE, 'stories'),
  'fit',
  WIDTH,
);
const { boxes, marks } = geometry;
// Every fixture edge joins two task bars (D4).
const edges = FINISHED_TIMELINE.edges;
const criticalPath = FINISHED_TIMELINE.criticalPath;
const labelled = edges.filter((e) => e.waitLabel !== null);
// An arrow into a story is never drawn: the story is a bracket, not a bar.
const intoStory: TimelineEdgeDto = {
  ...edges[0],
  to: 'SHIP-186',
  waitHours: 9.9,
  waitLabel: '9.9 h',
  onCriticalPath: false,
};

const layer = (
  over: {
    options?: Partial<TimelineOptions>;
    finished?: boolean;
    edges?: TimelineEdgeDto[];
  } = {},
) => (
  <svg width={WIDTH} height={geometry.height}>
    {/* Stand-ins for the marks lane's bars, so the test can read each mark's drawn box. */}
    {marks.map((m) => {
      const b = markBox(m);
      return (
        <rect
          key={m.key}
          data-testid={ids.chartBar}
          data-key={m.key}
          x={b.x0}
          y={b.y0}
          width={b.x1 - b.x0}
          height={b.y1 - b.y0}
          fill="none"
        />
      );
    })}
    <GanttEdgesLayer
      boxes={boxes}
      marks={marks}
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
  const label = page
    .getByTestId(ids.edgeWaitLabel)
    .and(page.locator('[data-from=SHIP-191][data-to=SHIP-192]'));
  await expect(label).toHaveText('22.8 h wait');
  await expect(label).toHaveAttribute('paint-order', 'stroke');
  await expect(page.locator('[data-from=SHIP-191][data-to=SHIP-192] title')).toHaveText(
    'SHIP-191 → SHIP-192, waited 22.8 h',
  );
});

test('GanttEdgesLayer_WhenFinished_ShouldDrawEachWaitOnceClearOfEveryBar', async ({
  mount,
  page,
}) => {
  await mount(layer());
  const texts = page.getByTestId(ids.edgeWaitLabel);
  await expect(texts.first()).toBeVisible();
  const drawn = await texts.allTextContents();
  expect(new Set(drawn).size).toBe(drawn.length);
  expect(drawn.every((t) => t.endsWith(' wait'))).toBe(true);

  const bars = page.getByTestId(ids.chartBar);
  await expect(bars).toHaveCount(marks.length);
  const barBoxes = await Promise.all((await bars.all()).map((b) => b.boundingBox()));
  for (const text of await texts.all()) {
    const t = (await text.boundingBox())!;
    for (const b of barBoxes) {
      const overlap =
        t.x < b!.x + b!.width &&
        b!.x < t.x + t.width &&
        t.y < b!.y + b!.height &&
        b!.y < t.y + t.height;
      expect(overlap, `${await text.textContent()} overlaps a bar`).toBe(false);
    }
  }
});

test('GanttEdgesLayer_WhenFinished_ShouldDrawOrTitleEveryWait', async ({ mount, page }) => {
  await mount(layer());
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  for (const e of labelled) {
    const text = page
      .getByTestId(ids.edgeWaitLabel)
      .and(page.locator(`[data-from="${e.from}"][data-to="${e.to}"]`));
    const title = page
      .getByTestId(ids.edge)
      .and(page.locator(`[data-from="${e.from}"][data-to="${e.to}"]`))
      .locator('title');
    if ((await text.count()) === 1) await expect(text).toHaveText(`${e.waitLabel} wait`);
    else await expect(title).toContainText(`waited ${e.waitLabel}`);
  }
});

test('GanttEdgesLayer_WhenAPairRepeats_ShouldDrawItOnce', async ({ mount, page }) => {
  await mount(layer({ edges: [...edges, ...edges] }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  const drawn = await page.getByTestId(ids.edgeWaitLabel).allTextContents();
  expect(drawn.length).toBeGreaterThan(0);
  expect(new Set(drawn).size).toBe(drawn.length);
});

test('GanttEdgesLayer_WhenAnEdgeEndsAtAStory_ShouldDrawNoArrowOrText', async ({ mount, page }) => {
  await mount(layer({ edges: [...edges, intoStory] }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  await expect(page.getByTestId(ids.edgeWaitLabel).first()).toBeVisible();
  await expect(page.locator('[data-to=SHIP-186]')).toHaveCount(0);
  await expect(page.getByText('9.9 h wait')).toHaveCount(0);
});

test('GanttEdgesLayer_WhenCpOn_ShouldFadeOffPathArrows', async ({ mount, page }) => {
  await mount(layer());
  const opacity = (from: string, to: string) =>
    page
      .getByTestId(ids.edge)
      .and(page.locator(`[data-from="${from}"][data-to="${to}"]`))
      .evaluate((el) => Number(getComputedStyle(el).opacity));
  expect(await opacity('SHIP-191', 'SHIP-193')).toBeLessThan(1);
  expect(await opacity('SHIP-191', 'SHIP-192')).toBe(1);
  // Blocked arrows stay red and dashed, never faded.
  expect(await opacity('SHIP-199', 'SHIP-196')).toBe(1);
});

test('GanttEdgesLayer_WhenCpOff_ShouldNotFade', async ({ mount, page }) => {
  await mount(layer({ options: { cp: false } }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  const faded = await page
    .getByTestId(ids.edge)
    .evaluateAll((els) => els.filter((el) => Number(getComputedStyle(el).opacity) < 1).length);
  expect(faded).toBe(0);
});
