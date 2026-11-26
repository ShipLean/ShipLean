import { expect, test } from '@playwright/experimental-ct-react';
import type { FeatureTimelineViewDto, TimelineEdgeDto } from '@shiplean/core/api/views';
import { GanttChart } from '../GanttChart';
import { LEFT_WIDTH } from '../GanttRowLabels';
import { DEFAULT_TIMELINE_OPTIONS, type TimelineOptions } from '../TimelineOptions';
import { timelineGeometry } from '../chart/timelineGeometry';
import { timelineRows } from '../chart/timelineRows';
import { timelineTestIds as ids } from '../testIds';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './stubFeatureTimeline';

// The real chart (marks lane and edges lane) on the finished fixture, with its scroll area 1800px wide: the
// wrapper adds the row labels' width (LEFT_WIDTH) and the chart's 1px border on each side.
const WIDTH = 1800;
const CHART_WIDTH = WIDTH + LEFT_WIDTH + 2;
test.use({ viewport: { width: CHART_WIDTH + 40, height: 900 } });

const { boxes } = timelineGeometry(
  FINISHED_TIMELINE,
  timelineRows(FINISHED_TIMELINE, 'stories'),
  'fit',
  WIDTH,
);
// Every fixture edge joins two task bars (D4).
const edges = FINISHED_TIMELINE.edges;
const criticalPath = FINISHED_TIMELINE.criticalPath;
const labelled = edges.filter((e) => e.waitLabel !== null);
const edge = (over: Partial<TimelineEdgeDto>): TimelineEdgeDto => ({
  ...edges[0],
  waitHours: 50,
  onCriticalPath: false,
  ...over,
});
// An arrow into a story is never drawn: the story is a bracket, not a task.
const intoStory = edge({ to: 'SHIP-186', waitHours: 9.9, waitLabel: '9.9 h' });
// Labelled arrows into and out of the finished fixture's diamond (SHIP-198), a task closed with no start.
const diamondEdges = [
  edge({ from: 'SHIP-197', to: 'SHIP-198', waitLabel: '8.2 days' }),
  edge({ from: 'SHIP-198', to: 'SHIP-196', waitLabel: '2.7 days' }),
];
// Labelled arrows into and out of the running fixture's open bar (SHIP-172).
const openBarEdges = [
  edge({ from: 'SHIP-121', to: 'SHIP-172', waitLabel: '28 days' }),
  edge({ from: 'SHIP-172', to: 'SHIP-122', waitLabel: '1 h' }),
];

// The CT package does not re-export Page and Locator; take them from the test's fixtures.
type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
type Locator = ReturnType<Page['locator']>;

const chart = (
  over: {
    view?: FeatureTimelineViewDto;
    options?: Partial<TimelineOptions>;
    finished?: boolean;
    edges?: TimelineEdgeDto[];
  } = {},
) => {
  const view = over.view ?? FINISHED_TIMELINE;
  return (
    <div style={{ width: CHART_WIDTH }}>
      <GanttChart
        view={{
          ...view,
          finished: over.finished ?? view.finished,
          edges: over.edges ?? view.edges,
        }}
        options={{ ...DEFAULT_TIMELINE_OPTIONS, ...over.options }}
        selected={null}
        onSelect={() => undefined}
      />
    </div>
  );
};

const arrow = (locator: Locator, from: string, to: string) =>
  locator.and(locator.page().locator(`[data-from="${from}"][data-to="${to}"]`));

/** Every drawn wait text whose box overlaps a drawn mark's box (bars with their end cues, diamonds with their notes, brackets, duration labels, notes and cues). */
async function overlapsWithMarks(page: Page) {
  const marks = page.locator(
    [
      ids.chartBar,
      ids.chartDiamond,
      ids.chartBracket,
      ids.chartDurationLabel,
      ids.chartInferredNote,
      ids.chartOpenEnd,
    ]
      .map((id) => `[data-testid="${id}"]`)
      .join(', '),
  );
  const markBoxes = await Promise.all(
    (await marks.all()).map(async (m) => ({
      what: `${await m.getAttribute('data-testid')} ${await m.getAttribute('data-key')}`,
      box: (await m.boundingBox())!,
    })),
  );
  const found: string[] = [];
  for (const text of await page.getByTestId(ids.edgeWaitLabel).all()) {
    const t = (await text.boundingBox())!;
    for (const { what, box: b } of markBoxes) {
      if (
        t.x < b.x + b.width &&
        b.x < t.x + t.width &&
        t.y < b.y + b.height &&
        b.y < t.y + t.height
      )
        found.push(`${await text.textContent()} overlaps ${what}`);
    }
  }
  return { found, marks: markBoxes.length };
}

test('GanttEdgesLayer_WhenDepsOff_ShouldDrawNothing', async ({ mount, page }) => {
  await mount(chart({ options: { deps: false } }));
  await expect(page.getByTestId(ids.edges)).toHaveAttribute('data-boxes', String(boxes.length));
  await expect(page.getByTestId(ids.edge)).toHaveCount(0);
  await expect(page.getByTestId(ids.edgeCritical)).toHaveCount(0);
});

test('GanttEdgesLayer_WhenBlocked_ShouldBeDashed', async ({ mount, page }) => {
  await mount(chart());
  const blocked = page.getByTestId(ids.edge).and(page.locator('[data-kind=blocked]'));
  await expect(blocked).toHaveCount(1);
  await expect(blocked).toHaveAttribute('data-from', 'SHIP-199');
  await expect(blocked.locator('path').first()).toHaveAttribute('stroke-dasharray', '4 3');
  const depends = page.locator('[data-kind=depends]').first();
  await expect(depends.locator('path').first()).not.toHaveAttribute('stroke-dasharray', /./);
});

test('GanttEdgesLayer_WhenFinishedAndCp_ShouldOutlineCriticalPath', async ({ mount, page }) => {
  await mount(chart());
  await expect(page.getByTestId(ids.edgeCritical)).toHaveCount(criticalPath.length);
  await expect(page.locator('[data-testid=timeline-edge][data-critical=true]')).toHaveCount(
    edges.filter((e) => e.onCriticalPath).length,
  );
});

test('GanttEdgesLayer_WhenCpOff_ShouldNotOutline', async ({ mount, page }) => {
  await mount(chart({ options: { cp: false } }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  await expect(page.getByTestId(ids.edgeCritical)).toHaveCount(0);
  await expect(page.locator('[data-testid=timeline-edge][data-critical=true]')).toHaveCount(0);
});

test('GanttEdgesLayer_WhenNotFinished_ShouldNotOutline', async ({ mount, page }) => {
  await mount(chart({ finished: false }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  await expect(page.getByTestId(ids.edgeCritical)).toHaveCount(0);
});

test('GanttEdgesLayer_ShouldShowWaitLabels', async ({ mount, page }) => {
  await mount(chart());
  const label = arrow(page.getByTestId(ids.edgeWaitLabel), 'SHIP-191', 'SHIP-192');
  await expect(label).toHaveText('22.8 h wait');
  await expect(label).toHaveAttribute('paint-order', 'stroke');
  await expect(
    arrow(page.getByTestId(ids.edge), 'SHIP-191', 'SHIP-192').locator('title'),
  ).toHaveText('SHIP-191 → SHIP-192, waited 22.8 h');
});

test('GanttEdgesLayer_WhenFinished_ShouldDrawEachWaitOnceClearOfEveryMark', async ({
  mount,
  page,
}) => {
  await mount(chart());
  expect(await page.getByTestId(ids.chartScroll).evaluate((el) => el.clientWidth)).toBe(WIDTH);
  const texts = page.getByTestId(ids.edgeWaitLabel);
  await expect(texts.first()).toBeVisible();
  const drawn = await texts.allTextContents();
  expect(new Set(drawn).size).toBe(drawn.length);
  expect(drawn.every((t) => t.endsWith(' wait'))).toBe(true);

  // The real marks: every bar, the diamond with its note, every bracket and duration label.
  await expect(page.getByTestId(ids.chartDiamond)).toHaveCount(1);
  await expect(page.getByTestId(ids.chartInferredNote)).toHaveText('Closed, no start recorded');
  await expect(page.getByTestId(ids.chartDurationLabel).first()).toBeVisible();
  const { found, marks } = await overlapsWithMarks(page);
  expect(marks).toBeGreaterThan(FINISHED_TIMELINE.bars.length + FINISHED_TIMELINE.stories.length);
  expect(found).toEqual([]);
});

test('GanttEdgesLayer_WhenArrowsJoinADiamond_ShouldDrawThemClearOfItsNote', async ({
  mount,
  page,
}) => {
  await mount(chart({ edges: [...edges, ...diamondEdges] }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length + diamondEdges.length);
  const into = arrow(page.getByTestId(ids.edge), 'SHIP-197', 'SHIP-198');
  await expect(into).toHaveCount(1);
  await expect(arrow(page.getByTestId(ids.edge), 'SHIP-198', 'SHIP-196')).toHaveCount(1);
  // The arrow ends at the diamond's left point, the last point GanttMarks draws it with.
  const points = (await page
    .getByTestId(ids.chartDiamond)
    .locator('polygon')
    .getAttribute('points'))!;
  const leftX = Number(points.trim().split(/\s+/).at(-1)!.split(',')[0]);
  const d = (await into.locator('path').first().getAttribute('d'))!;
  expect(Number(d.split('H').at(-1))).toBeCloseTo(leftX, 3);
  await expect(page.getByTestId(ids.chartInferredNote)).toBeVisible();
  const { found } = await overlapsWithMarks(page);
  expect(found).toEqual([]);
});

test('GanttEdgesLayer_WhenArrowsJoinAnOpenBar_ShouldDrawLabelsClearOfItsEndCue', async ({
  mount,
  page,
}) => {
  await mount(
    chart({ view: RUNNING_TIMELINE, edges: [...RUNNING_TIMELINE.edges, ...openBarEdges] }),
  );
  await expect(page.getByTestId(ids.chartOpenEnd)).toHaveCount(1);
  await expect(page.getByTestId(ids.edge)).toHaveCount(
    RUNNING_TIMELINE.edges.length + openBarEdges.length,
  );
  await expect(page.getByTestId(ids.edgeWaitLabel).first()).toBeVisible();
  const { found } = await overlapsWithMarks(page);
  expect(found).toEqual([]);
});

test('GanttEdgesLayer_WhenFinished_ShouldDrawOrTitleEveryWait', async ({ mount, page }) => {
  await mount(chart());
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  for (const e of labelled) {
    const text = arrow(page.getByTestId(ids.edgeWaitLabel), e.from, e.to);
    const title = arrow(page.getByTestId(ids.edge), e.from, e.to).locator('title');
    if ((await text.count()) === 1) await expect(text).toHaveText(`${e.waitLabel} wait`);
    else await expect(title).toContainText(`waited ${e.waitLabel}`);
  }
});

test('GanttEdgesLayer_WhenAPairRepeats_ShouldDrawItOnce', async ({ mount, page }) => {
  await mount(chart({ edges: [...edges, ...edges] }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  const drawn = await page.getByTestId(ids.edgeWaitLabel).allTextContents();
  expect(drawn.length).toBeGreaterThan(0);
  expect(new Set(drawn).size).toBe(drawn.length);
});

test('GanttEdgesLayer_WhenAnEdgeEndsAtAStory_ShouldDrawNoArrowOrText', async ({ mount, page }) => {
  await mount(chart({ edges: [...edges, intoStory] }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  await expect(page.getByTestId(ids.edgeWaitLabel).first()).toBeVisible();
  await expect(page.locator('[data-to=SHIP-186]')).toHaveCount(0);
  await expect(page.getByText('9.9 h wait')).toHaveCount(0);
});

test('GanttEdgesLayer_WhenCpOn_ShouldFadeOffPathArrows', async ({ mount, page }) => {
  // An off-path blocked arrow fades like any off-path arrow, staying red and dashed.
  const offPathBlocked = edge({
    from: 'SHIP-193',
    to: 'SHIP-197',
    kind: 'blocked',
    waitHours: null,
    waitLabel: null,
  });
  await mount(chart({ edges: [...edges, offPathBlocked] }));
  const opacity = (from: string, to: string) =>
    arrow(page.getByTestId(ids.edge), from, to).evaluate((el) =>
      Number(getComputedStyle(el).opacity),
    );
  expect(await opacity('SHIP-191', 'SHIP-193')).toBeLessThan(1);
  expect(await opacity('SHIP-193', 'SHIP-197')).toBeLessThan(1);
  const blocked = arrow(page.getByTestId(ids.edge), 'SHIP-193', 'SHIP-197').locator('path').first();
  await expect(blocked).toHaveAttribute('stroke', 'var(--color-danger)');
  await expect(blocked).toHaveAttribute('stroke-dasharray', '4 3');
  // Only arrows on the path stay at full strength, the blocked one on it too.
  expect(await opacity('SHIP-191', 'SHIP-192')).toBe(1);
  expect(await opacity('SHIP-199', 'SHIP-196')).toBe(1);
});

test('GanttEdgesLayer_WhenCpOff_ShouldNotFade', async ({ mount, page }) => {
  await mount(chart({ options: { cp: false } }));
  await expect(page.getByTestId(ids.edge)).toHaveCount(edges.length);
  const faded = await page
    .getByTestId(ids.edge)
    .evaluateAll((els) => els.filter((el) => Number(getComputedStyle(el).opacity) < 1).length);
  expect(faded).toBe(0);
});

test('GanttEdgesLayer_WhenFit_ShouldDrawEveryWaitLabelInsideTheSvg', async ({ mount, page }) => {
  await mount(chart());
  const texts = page.getByTestId(ids.edgeWaitLabel);
  await expect(texts.first()).toBeVisible();
  const svgRight = (await page.getByTestId(ids.chartScroll).locator('svg').first().boundingBox())!;
  const limit = svgRight.x + svgRight.width;
  const count = await texts.count();
  expect(count).toBeGreaterThan(0);
  for (let i = 0; i < count; i++) {
    const box = (await texts.nth(i).boundingBox())!;
    expect(box.x + box.width).toBeLessThanOrEqual(limit);
  }
});
