import { expect, test } from '@playwright/experimental-ct-react';
import type { InspectionPeriodDto } from '@shiplean/core/api/views';
import fixture from '../../../../../ShipLean.TestSupport/DtoFixtures/views/inspection-metrics-view.json' with { type: 'json' };
import { LeadTimeChart } from '../chart/LeadTimeChart';
import { inspectionMetricsTestIds as ids } from '../testIds';

// The periods as the server sends them in T0's pinned view (inspection-metrics-view.json).
const PERIODS = fixture.periods as unknown as InspectionPeriodDto[];
const RUNNING = PERIODS.find((p) => p.inProgress)!;
const LONGEST = PERIODS.flatMap((p) => p.dots).find((d) => d.longest)!;

type Mount = Parameters<Parameters<typeof test>[2]>[0]['mount'];

const mountChart = (
  mount: Mount,
  periods: InspectionPeriodDto[],
  sent: { periods?: string[]; dots?: string[] } = {},
) =>
  mount(
    <div style={{ width: 900 }}>
      <LeadTimeChart
        periods={periods}
        onPeriod={(id) => sent.periods?.push(id)}
        onDot={(key) => sent.dots?.push(key)}
      />
    </div>,
  );

test('LeadTimeChart_WhenPeriods_ShouldDrawOneBarPerPeriod', async ({ mount, page }) => {
  await mountChart(mount, PERIODS);
  const bars = page.getByTestId(ids.chartBar);
  await expect(bars).toHaveCount(PERIODS.length);
  for (const [i, period] of PERIODS.entries())
    await expect(bars.nth(i)).toHaveAttribute('data-period-id', period.id);
  await expect(page.getByTestId(ids.chartDot)).toHaveCount(
    PERIODS.reduce((n, p) => n + p.dots.length, 0),
  );
});

test('LeadTimeChart_WhenOneRunning_ShouldMarkOnlyItSoFar', async ({ mount, page }) => {
  await mountChart(mount, PERIODS);
  await expect(page.getByTestId(ids.chartSoFar)).toHaveCount(1);
  const running = page
    .getByTestId(ids.chartBar)
    .and(page.locator(`[data-period-id="${RUNNING.id}"]`));
  await expect(running.getByTestId(ids.chartSoFar)).toHaveCount(1);
});

test('LeadTimeChart_WhenDotOverTwoDays_ShouldLabelTheLongestOnTheTopEdge', async ({
  mount,
  page,
}) => {
  await mountChart(mount, PERIODS);
  const label = page.getByTestId(ids.chartDotLabel);
  await expect(label).toHaveCount(1);
  await expect(label).toHaveText(`${LONGEST.taskKey} ${LONGEST.leadLabel}`);
  await expect(
    page.getByTestId(ids.chartDot).and(page.locator(`[data-key="${LONGEST.taskKey}"]`)),
  ).toHaveAttribute('data-over', 'true');
});

test('LeadTimeChart_WhenBarClicked_ShouldCallOnPeriodWithItsId', async ({ mount, page }) => {
  const sent = { periods: [] as string[], dots: [] as string[] };
  await mountChart(mount, PERIODS, sent);
  const target = PERIODS[1];
  // The column's lower edge: below the bars' dots, above the period labels.
  await page
    .getByTestId(ids.chartBar)
    .and(page.locator(`[data-period-id="${target.id}"]`))
    .click({ position: { x: 10, y: 20 } });
  await expect.poll(() => sent.periods).toEqual([target.id]);
  expect(sent.dots).toEqual([]);
});

test('LeadTimeChart_WhenDotClicked_ShouldCallOnDotWithItsKey', async ({ mount, page }) => {
  const sent = { periods: [] as string[], dots: [] as string[] };
  await mountChart(mount, PERIODS, sent);
  await page
    .getByTestId(ids.chartDot)
    .and(page.locator(`[data-key="${LONGEST.taskKey}"]`))
    .click({ force: true });
  await expect.poll(() => sent.dots).toEqual([LONGEST.taskKey]);
  expect(sent.periods).toEqual([]);
});

test('LeadTimeChart_WhenEnterOnFocusedBar_ShouldCallOnPeriod', async ({ mount, page }) => {
  const sent = { periods: [] as string[] };
  await mountChart(mount, PERIODS, sent);
  await page.getByTestId(ids.chartBar).first().focus();
  await page.keyboard.press('Enter');
  await expect.poll(() => sent.periods).toEqual([PERIODS[0].id]);
});

test('LeadTimeChart_WhenNoPeriods_ShouldShowTheEmptyState', async ({ mount, page }) => {
  await mountChart(mount, []);
  await expect(page.getByTestId(ids.chartEmpty)).toBeVisible();
  await expect(page.getByTestId(ids.chart)).toHaveCount(0);
  await expect(page.getByTestId(ids.chartBar)).toHaveCount(0);
});

for (const scheme of ['light', 'dark'] as const) {
  test(`LeadTimeChart_WhenRendered_ShouldUseTheThemeColours_${scheme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: 'reduce' });
    await mountChart(mount, PERIODS);
    const over = page.getByTestId(ids.chartDot).and(page.locator('[data-over="true"]')).first();
    await expect(over).toBeVisible();
    const [fill, danger] = await over.evaluate((el) => {
      // Resolve the token through a probe element, so both colours come back in the same rgb() form.
      const probe = document.createElement('span');
      probe.style.color = 'var(--color-danger)';
      document.body.append(probe);
      const colours = [getComputedStyle(el).fill, getComputedStyle(probe).color];
      probe.remove();
      return colours;
    });
    expect(fill).toBe(danger);
    expect(fill).toBe(scheme === 'light' ? 'rgb(194, 53, 59)' : 'rgb(240, 138, 143)');
  });
}
