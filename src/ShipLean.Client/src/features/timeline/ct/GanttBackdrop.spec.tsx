import { expect, test } from '@playwright/experimental-ct-react';
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { GanttChart } from '../GanttChart';
import { StatefulGanttChart } from './StatefulGanttChart';
import { DEFAULT_TIMELINE_OPTIONS, type TimelineOptions } from '../TimelineOptions';
import { ROW_HEIGHT, rowY } from '../chart/timelineGeometry';
import { timelineTestIds as ids } from '../testIds';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './stubFeatureTimeline';

const mountChart = (
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  view: FeatureTimelineViewDto,
  extra: { options?: Partial<TimelineOptions>; selected?: string | null } = {},
) =>
  mount(
    <>
      <GanttChart
        view={view}
        options={{ ...DEFAULT_TIMELINE_OPTIONS, ...extra.options }}
        selected={extra.selected ?? null}
        onSelect={() => undefined}
      />
    </>,
  );

test('GanttBackdrop_WhenFit_ShouldLabelWeeksAndNoBareDayNumbers', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE);
  const weeks = FINISHED_TIMELINE.scale.days.filter((d) => d.weekStart).map((d) => d.shortLabel);
  const labels = page.getByTestId(ids.chartDayLabel);
  await expect(labels).toHaveText(weeks);
  await expect(page.getByTestId(ids.chartWeekTick)).toHaveCount(weeks.length);
});

test('GanttBackdrop_WhenZoomIsDays_ShouldShowDayLabels', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE, { options: { zoom: 'days' } });
  await expect(page.getByTestId(ids.chartDayLabel)).toHaveText(
    FINISHED_TIMELINE.scale.days.map((d) => d.label),
  );
});

test('GanttBackdrop_WhenRunning_ShouldDrawNowLineInAccentNotDanger', async ({ mount, page }) => {
  await mountChart(mount, RUNNING_TIMELINE);
  await expect(page.getByTestId(ids.chartNowLabel)).toHaveText('Now');
  const line = page.getByTestId(ids.chartNowLine);
  await expect(line).toHaveCount(1);
  const [stroke, danger] = await Promise.all([
    line.evaluate((el) => getComputedStyle(el).stroke),
    page.evaluate(() => {
      const probe = document.createElement('span');
      probe.style.color = 'var(--color-danger)';
      document.body.appendChild(probe);
      const colour = getComputedStyle(probe).color;
      probe.remove();
      return colour;
    }),
  ]);
  expect(stroke).not.toBe(danger);
  expect(stroke).not.toBe('none');
});

test('GanttBackdrop_WhenARowIsSelected_ShouldDrawTheBandAtItsRowAcrossTheChart', async ({
  mount,
  page,
}) => {
  await mount(<StatefulGanttChart view={FINISHED_TIMELINE} />);
  const row = page.getByTestId(ids.chartBar).and(page.locator('[data-key="SHIP-192"]'));
  await row.click();
  const band = page.getByTestId(ids.chartSelectedBand);
  await expect(band).toHaveCount(1);
  const svgWidth = await page
    .getByRole('img', { name: /Timeline of/ })
    .evaluate((el) => Number(el.getAttribute('width')));
  const [y, width, height] = await Promise.all([
    band.getAttribute('y'),
    band.getAttribute('width'),
    band.getAttribute('height'),
  ]);
  expect(Number(width)).toBe(svgWidth);
  expect(Number(height)).toBe(ROW_HEIGHT);
  expect((Number(y) - rowY(0)) % ROW_HEIGHT).toBe(0);
  const barBox = await row.boundingBox();
  const bandBox = await band.boundingBox();
  expect(barBox!.y).toBeGreaterThanOrEqual(bandBox!.y);
  expect(barBox!.y + barBox!.height).toBeLessThanOrEqual(bandBox!.y + bandBox!.height);
});

test('GanttBackdrop_WhenEmptySpaceInSelectedRowClicked_ShouldClearSelection', async ({
  mount,
  page,
}) => {
  await mount(<StatefulGanttChart view={FINISHED_TIMELINE} />);
  await page.getByTestId(ids.chartBar).and(page.locator('[data-key="SHIP-192"]')).click();
  const band = page.getByTestId(ids.chartSelectedBand);
  await expect(band).toHaveCount(1);
  const box = (await band.boundingBox())!;
  await page.mouse.click(box.x + box.width - 3, box.y + box.height / 2);
  await expect(band).toHaveCount(0);
});
