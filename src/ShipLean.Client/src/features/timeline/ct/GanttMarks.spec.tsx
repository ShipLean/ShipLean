import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright';
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { GanttChart } from '../GanttChart';
import { DEFAULT_TIMELINE_OPTIONS, type TimelineOptions } from '../TimelineOptions';
import { durationLabelBox } from '../chart/labelBoxes';
import type { Mark } from '../chart/timelineGeometry';
import { timelineTestIds as ids } from '../testIds';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './stubFeatureTimeline';

const mountChart = (
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  view: FeatureTimelineViewDto,
  options: Partial<TimelineOptions> = {},
) =>
  mount(
    <GanttChart
      view={view}
      options={{ ...DEFAULT_TIMELINE_OPTIONS, ...options }}
      selected={null}
      onSelect={() => undefined}
    />,
  );

const keyed = (locator: Locator, key: string) =>
  locator.and(locator.page().locator(`[data-key="${key}"]`));

/** The opacity the user sees: the element's own times every ancestor's. */
const seenOpacity = (locator: Locator) =>
  locator.evaluate((el: Element) => {
    let opacity = 1;
    for (let node: Element | null = el; node !== null; node = node.parentElement)
      opacity *= Number(getComputedStyle(node).opacity);
    return opacity;
  });

const finishedBar = (key: string) => FINISHED_TIMELINE.bars.find((b) => b.key === key)!;
const finishedStory = (key: string) => FINISHED_TIMELINE.stories.find((s) => s.key === key)!;

test('GanttMarks_WhenBarHasDuration_ShouldDrawItsLabelAtTheLabelBox', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE);
  const bar = finishedBar('SHIP-192');
  const rect = keyed(page.getByTestId(ids.chartBar), 'SHIP-192').locator('rect');
  const dots = keyed(page.getByTestId(ids.chartReviewDot), 'SHIP-192');
  await expect(dots).toHaveCount(bar.reviews.length);
  const num = async (locator: Locator, name: string) => Number(await locator.getAttribute(name));
  const x0 = await num(rect, 'x');
  const reviews: Mark['reviews'] = [];
  for (const [i, dot] of (await dots.all()).entries())
    reviews.push({ x: await num(dot, 'cx'), verdict: bar.reviews[i]!.verdict, atLabel: '' });
  const mark: Mark = {
    key: bar.key,
    kind: 'bar',
    row: 0,
    box: {
      key: bar.key,
      x0,
      x1: x0 + (await num(rect, 'width')),
      y: await num(rect, 'y'),
      height: await num(rect, 'height'),
    },
    reviews,
    reviewLine: null,
    durationLabel: bar.durationLabel,
  };
  const label = keyed(page.getByTestId(ids.chartDurationLabel), 'SHIP-192');
  await expect(label).toHaveText(bar.durationLabel!);
  expect(await num(label, 'x')).toBeCloseTo(durationLabelBox(mark, bar.durationLabel!)!.x0, 3);
});

test('GanttMarks_WhenStoryHasDuration_ShouldDrawItsLabelAfterTheBracket', async ({
  mount,
  page,
}) => {
  await mountChart(mount, FINISHED_TIMELINE);
  await expect(keyed(page.getByTestId(ids.chartDurationLabel), 'SHIP-185')).toHaveText(
    finishedStory('SHIP-185').durationLabel!,
  );
});

test('GanttMarks_WhenStory_ShouldDrawBracketAsPathWithNoFill', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE);
  const bracket = keyed(page.getByTestId(ids.chartBracket), 'SHIP-185');
  await expect(bracket.locator('path')).toHaveCount(1);
  await expect(bracket.locator('rect')).toHaveCount(0);
  await expect(bracket.locator('path')).toHaveCSS('fill', 'none');
  await expect(bracket.locator('path')).toHaveCSS('stroke-dasharray', 'none');
});

test('GanttMarks_WhenStoryOpen_ShouldDashItsBracket', async ({ mount, page }) => {
  await mountChart(mount, RUNNING_TIMELINE);
  const bracket = keyed(page.getByTestId(ids.chartBracket), 'SHIP-141').locator('path');
  await expect(bracket).toHaveCount(1);
  await expect(bracket).not.toHaveCSS('stroke-dasharray', 'none');
});

test('GanttMarks_WhenInferred_ShouldDrawDashedDiamondWithNoDuration', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE);
  const diamond = keyed(page.getByTestId(ids.chartDiamond), 'SHIP-198');
  await expect(diamond).toContainText('Closed, no start recorded');
  await expect(diamond.locator('polygon')).not.toHaveCSS('stroke-dasharray', 'none');
  await expect(keyed(page.getByTestId(ids.chartDurationLabel), 'SHIP-197')).toHaveCount(1);
  await expect(keyed(page.getByTestId(ids.chartDurationLabel), 'SHIP-198')).toHaveCount(0);
});

test('GanttMarks_WhenCriticalPathOn_ShouldFadeOffPathMarks', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE, { cp: true });
  const bars = page.getByTestId(ids.chartBar);
  const labels = page.getByTestId(ids.chartDurationLabel);
  const brackets = page.getByTestId(ids.chartBracket);
  await expect(keyed(bars, 'SHIP-193')).toBeVisible();
  expect(await seenOpacity(keyed(bars, 'SHIP-193'))).toBeLessThan(1);
  expect(await seenOpacity(keyed(labels, 'SHIP-193'))).toBeLessThan(1);
  expect(await seenOpacity(keyed(bars, 'SHIP-192'))).toBe(1);
  expect(await seenOpacity(keyed(labels, 'SHIP-192'))).toBe(1);
  // SHIP-185's tasks SHIP-191 and SHIP-192 are on the path; none of SHIP-188's are.
  expect(await seenOpacity(keyed(brackets, 'SHIP-185'))).toBe(1);
  expect(await seenOpacity(keyed(brackets, 'SHIP-188'))).toBeLessThan(1);
});

test('GanttMarks_WhenCriticalPathOff_ShouldFadeNothing', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE, { cp: false });
  const bars = page.getByTestId(ids.chartBar);
  const labels = page.getByTestId(ids.chartDurationLabel);
  await expect(keyed(bars, 'SHIP-193')).toBeVisible();
  for (const key of ['SHIP-192', 'SHIP-193']) {
    expect(await seenOpacity(keyed(bars, key))).toBe(1);
    expect(await seenOpacity(keyed(labels, key))).toBe(1);
  }
  expect(await seenOpacity(keyed(page.getByTestId(ids.chartBracket), 'SHIP-188'))).toBe(1);
});

test('GanttMarks_WhenBarOpen_ShouldDrawEndCue', async ({ mount, page }) => {
  await mountChart(mount, RUNNING_TIMELINE);
  const bars = page.getByTestId(ids.chartBar);
  await expect(keyed(bars, 'SHIP-171')).toBeVisible();
  await expect(keyed(bars, 'SHIP-172').getByTestId(ids.chartOpenEnd)).toHaveCount(1);
  await expect(keyed(bars, 'SHIP-171').getByTestId(ids.chartOpenEnd)).toHaveCount(0);
});
