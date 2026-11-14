import { expect, test } from '@playwright/experimental-ct-react';
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { GanttChart } from '../GanttChart';
import { DEFAULT_TIMELINE_OPTIONS, type TimelineOptions } from '../TimelineOptions';
import { timelineTestIds as ids } from '../testIds';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './stubFeatureTimeline';

const mountChart = (
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  view: FeatureTimelineViewDto,
  extra: {
    options?: Partial<TimelineOptions>;
    selected?: string | null;
    picks?: (string | null)[];
  } = {},
) =>
  mount(
    <GanttChart
      view={view}
      options={{ ...DEFAULT_TIMELINE_OPTIONS, ...extra.options }}
      selected={extra.selected ?? null}
      onSelect={(key) => extra.picks?.push(key)}
    />,
  );

test('GanttChart_WhenFinished_ShouldDrawEveryBarAndBracket', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE);
  const inferred = FINISHED_TIMELINE.bars.filter((b) => b.inferred).length;
  await expect(page.getByTestId(ids.chartBar)).toHaveCount(
    FINISHED_TIMELINE.bars.length - inferred,
  );
  await expect(page.getByTestId(ids.chartBracket)).toHaveCount(FINISHED_TIMELINE.stories.length);
  await expect(page.getByTestId(ids.chartRow)).toHaveCount(
    FINISHED_TIMELINE.bars.length + FINISHED_TIMELINE.stories.length,
  );
  await expect(page.getByTestId(ids.chartNowLine)).toHaveCount(0);
});

test('GanttChart_WhenInferred_ShouldDrawDiamond', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE);
  const diamond = page.getByTestId(ids.chartDiamond);
  await expect(diamond).toHaveCount(1);
  await expect(diamond).toHaveAttribute('data-key', 'SHIP-198');
});

test('GanttChart_WhenReviewed_ShouldDrawDotsAfterBar', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE);
  const bar = page.getByTestId(ids.chartBar).and(page.locator('[data-key="SHIP-191"]'));
  const dots = page.getByTestId(ids.chartReviewDot).and(page.locator('[data-key="SHIP-191"]'));
  await expect(dots).toHaveCount(1);
  const barBox = (await bar.boundingBox())!;
  for (const dot of await dots.all()) {
    expect((await dot.boundingBox())!.x).toBeGreaterThanOrEqual(barBox.x + barBox.width - 1);
  }
});

test('GanttChart_WhenBarClicked_ShouldCallOnSelect', async ({ mount, page }) => {
  const picks: (string | null)[] = [];
  await mountChart(mount, FINISHED_TIMELINE, { picks });
  await page.getByTestId(ids.chartBar).and(page.locator('[data-key="SHIP-194"]')).click();
  expect(picks).toEqual(['SHIP-194']);
});

test('GanttChart_WhenEscPressed_ShouldClearSelection', async ({ mount, page }) => {
  const picks: (string | null)[] = [];
  await mountChart(mount, FINISHED_TIMELINE, { picks, selected: 'SHIP-194' });
  await page.keyboard.press('Escape');
  await expect.poll(() => picks).toEqual([null]);
});

test('GanttChart_WhenEnterOnFocusedBar_ShouldSelect', async ({ mount, page }) => {
  const picks: (string | null)[] = [];
  await mountChart(mount, FINISHED_TIMELINE, { picks });
  await page.getByTestId(ids.chartBar).and(page.locator('[data-key="SHIP-195"]')).focus();
  await page.keyboard.press('Enter');
  expect(picks).toEqual(['SHIP-195']);
});

test('GanttChart_WhenStoryBracketClicked_ShouldSelectStory', async ({ mount, page }) => {
  const picks: (string | null)[] = [];
  await mountChart(mount, FINISHED_TIMELINE, { picks });
  await page.getByTestId(ids.chartBracket).and(page.locator('[data-key="SHIP-186"]')).click();
  expect(picks).toEqual(['SHIP-186']);
});

test('GanttChart_WhenEmptySpaceClicked_ShouldClearSelection', async ({ mount, page }) => {
  const picks: (string | null)[] = [];
  await mountChart(mount, FINISHED_TIMELINE, { picks, selected: 'SHIP-194' });
  const scroll = (await page.getByTestId(ids.chartScroll).boundingBox())!;
  await page.mouse.click(scroll.x + scroll.width - 3, scroll.y + 25);
  await expect.poll(() => picks).toEqual([null]);
});

test('GanttChart_WhenRunning_ShouldDrawNowLine', async ({ mount, page }) => {
  await mountChart(mount, RUNNING_TIMELINE);
  await expect(page.getByTestId(ids.chartNowLine)).toHaveCount(1);
});

test('GanttChart_ShouldShadeWeekends', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE);
  await expect(page.getByTestId(ids.chartWeekend)).toHaveCount(
    FINISHED_TIMELINE.scale.days.filter((d) => d.weekend).length,
  );
});

test('GanttChart_WhenGroupPeople_ShouldGroupByAssignee', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE, { options: { group: 'people' } });
  const names = new Set(FINISHED_TIMELINE.bars.map((b) => b.assignee?.name));
  const groups = page.getByTestId(ids.chartRow).and(page.locator('[data-kind="group"]'));
  await expect(groups).toHaveCount(names.size);
  await expect(page.getByTestId(ids.chartBracket)).toHaveCount(0);
});

test('GanttChart_WhenEmpty_ShouldSayNothingStarted', async ({ mount, page }) => {
  await mountChart(mount, { ...FINISHED_TIMELINE, bars: [], stories: [] });
  await expect(page.getByText('Nothing has started yet.')).toBeVisible();
  await expect(page.getByTestId(ids.chartBar)).toHaveCount(0);
});

test('GanttChart_ShouldPassBoxesToEdgesLayer', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE);
  await expect(page.getByTestId(ids.edges)).toHaveAttribute(
    'data-boxes',
    String(FINISHED_TIMELINE.bars.length + FINISHED_TIMELINE.stories.length),
  );
});

const shots: { name: string; view: FeatureTimelineViewDto; options: Partial<TimelineOptions> }[] = [
  { name: 'finished', view: FINISHED_TIMELINE, options: {} },
  { name: 'people', view: FINISHED_TIMELINE, options: { group: 'people' } },
  { name: 'days', view: FINISHED_TIMELINE, options: { zoom: 'days' } },
  { name: 'running', view: RUNNING_TIMELINE, options: {} },
];
for (const theme of ['light', 'dark'] as const) {
  for (const shot of shots) {
    test(`GanttChart_WhenRendered_ShouldMatchItsScreenshot_${shot.name}_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      const chart = await mountChart(mount, shot.view, { options: shot.options });
      await expect(page.getByTestId(ids.chartBar).first()).toBeVisible();
      await expect(chart).toHaveScreenshot(`gantt-chart-${shot.name}.${theme}.png`, {
        animations: 'disabled',
      });
    });
  }
}
