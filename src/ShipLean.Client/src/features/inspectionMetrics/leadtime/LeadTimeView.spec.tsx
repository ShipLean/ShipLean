import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubProjectApi } from '../../../pages/pagesFixtures';
import { routeInspectionMetrics, stubCut, stubView } from '../ct/stubInspectionMetrics';
import { inspectionMetricsTestIds as ids } from '../testIds';

const ROUTE = '/p/SHIP/inspection?view=leadtime';
const VIEW = stubView();
// The fixture's sprint with all three parts (active 7.5 h, blocked 45.6 h, rework 10.6 h of 63.7 h).
const SPLIT = VIEW.periods.find((p) => (p.leadTime.meanBlockedHours ?? 0) > 0)!;

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
type Mount = Parameters<Parameters<typeof test>[2]>[0]['mount'];

async function open(mount: Mount, page: Page, route = ROUTE, view = VIEW) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await stubProjectApi(page);
  const server = await routeInspectionMetrics(page, view);
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.leadTime)).toBeVisible();
  return server;
}

const row = (page: Page, periodId: string) =>
  page.getByTestId(ids.periodRow).and(page.locator(`[data-period-id="${periodId}"]`));

for (const theme of ['light', 'dark'] as const) {
  test(`LeadTimeView_WhenASprintHasEveryPart_ShouldSplitItsBarByThePartsMeans_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(mount, page);
    const bar = row(page, SPLIT.id).getByTestId(ids.periodRowSplit);
    const barWidth = (await bar.boundingBox())!.width;
    const stats = SPLIT.leadTime;
    const expected = {
      active: stats.meanActiveHours!,
      blocked: stats.meanBlockedHours!,
      rework: stats.meanReworkHours!,
    };
    const total = expected.active + expected.blocked + expected.rework;
    for (const [kind, hours] of Object.entries(expected)) {
      const segment = bar.getByTestId(ids.splitSegment).and(page.locator(`[data-kind="${kind}"]`));
      const width = (await segment.boundingBox())!.width;
      expect(width / barWidth).toBeCloseTo(hours / total, 2);
    }
    // Blocked is drawn in the danger colour of the theme.
    const blocked = bar.getByTestId(ids.splitSegment).and(page.locator('[data-kind="blocked"]'));
    await expect(blocked).toHaveCSS(
      'background-color',
      theme === 'light' ? 'rgb(194, 53, 59)' : 'rgb(240, 138, 143)',
    );
  });

  test(`LeadTimeView_WhenQuartersRollUp_ShouldStyleOnlyTheCurrentOneToDate_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(mount, page);
    const quarters = page.getByTestId(ids.quarterRow);
    const shown = [...new Set(VIEW.periods.map((p) => p.quarter))];
    await expect(quarters).toHaveCount(shown.length);
    const toDate = VIEW.quarters.filter((q) => q.toDate && shown.includes(q.quarter));
    expect(toDate).toHaveLength(1);
    await expect(
      page.locator(`[data-testid="${ids.quarterRow}"][data-to-date="true"]`),
    ).toHaveCount(1);
    for (const quarter of VIEW.quarters.filter((q) => shown.includes(q.quarter))) {
      const r = quarters.and(page.locator(`[data-quarter="${quarter.quarter}"]`));
      if (quarter.toDate) {
        await expect(r).toHaveAttribute('data-to-date', 'true');
        await expect(r).toContainText(`${quarter.quarter} to date`);
      } else {
        await expect(r).not.toHaveAttribute('data-to-date');
        await expect(r).not.toContainText('to date');
      }
    }
    // Each quarter's row follows its last sprint.
    const order = await page
      .locator(`[data-testid="${ids.periodRow}"], [data-testid="${ids.quarterRow}"]`)
      .evaluateAll((els) =>
        els.map(
          (el) => el.getAttribute('data-period-id') ?? `Q:${el.getAttribute('data-quarter')}`,
        ),
      );
    const expectedOrder = VIEW.periods.flatMap((p, i) =>
      VIEW.periods[i + 1]?.quarter === p.quarter ? [p.id] : [p.id, `Q:${p.quarter}`],
    );
    expect(order).toEqual(expectedOrder);
  });

  for (const cut of ['feature', 'who'] as const) {
    test(`LeadTimeView_WhenCutIs_${cut}_ShouldDrawOneColumnPerPeriod_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      const view = stubCut(cut);
      await open(mount, page, `${ROUTE}&cut=${cut}`, view);
      await expect(page.getByTestId(ids.cutTable)).toHaveAttribute('data-cut', cut);
      await expect(page.getByTestId(ids.periodRows)).toHaveCount(0);
      const rows = page.getByTestId(ids.cutRow);
      await expect(rows).toHaveCount(view.cuts!.length);
      for (let i = 0; i < view.cuts!.length; i++)
        await expect(rows.nth(i).getByTestId(ids.cutCell)).toHaveCount(view.periods.length);
      await expect(
        page.locator(`[data-testid="${ids.cutOption}"][data-value="${cut}"]`),
      ).toHaveAttribute('aria-pressed', 'true');
    });
  }
}

test('LeadTimeView_WhenRangeAndCutPicked_ShouldWriteThemToTheAddress', async ({ mount, page }) => {
  const server = await open(mount, page);
  await expect.poll(() => server.reads.length).toBe(1);

  await page.getByTestId(ids.rangePicker).selectOption('all');
  await expect.poll(() => server.reads.at(-1)).toContain('range=all&cut=none');
  await expect(page.getByTestId(ids.rangePicker)).toHaveValue('all');

  await page.locator(`[data-testid="${ids.cutOption}"][data-value="who"]`).click();
  await expect.poll(() => server.reads.at(-1)).toContain('range=all&cut=who');
  await expect(page.locator(`[data-testid="${ids.cutOption}"][data-value="who"]`)).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  await page.locator(`[data-testid="${ids.cutOption}"][data-value="none"]`).click();
  await expect.poll(() => server.reads.at(-1)).toContain('range=all&cut=none');
});

test('LeadTimeView_WhenASprintRowIsClicked_ShouldOpenThatPeriod', async ({ mount, page }) => {
  const server = await open(mount, page);
  await row(page, SPLIT.id).click();
  await expect(page.getByTestId(ids.period)).toBeAttached();
  expect(server.reads.at(-1)).toBe(
    `/api/projects/id-SHIP/inspection-metrics?range=last6&cut=none&period=${SPLIT.id}`,
  );
});

test('LeadTimeView_WhenADotIsClicked_ShouldOpenItsPeriodWithTheTaskAndPostTaskOpened', async ({
  mount,
  page,
}) => {
  const server = await open(mount, page);
  const dot = SPLIT.dots[0]!;
  await page
    .getByTestId(ids.chartDot)
    .and(page.locator(`[data-key="${dot.taskKey}"]`))
    .click();
  await expect(page.getByTestId(ids.period)).toBeAttached();
  expect(server.reads.at(-1)).toBe(
    `/api/projects/id-SHIP/inspection-metrics?range=last6&cut=none&period=${SPLIT.id}&select=${dot.taskKey}`,
  );
  await expect
    .poll(() => server.usage.filter((u) => u.path === 'inspection-metrics-task-opened'))
    .toEqual([{ path: 'inspection-metrics-task-opened', body: { from: 'chart' } }]);
});

test('LeadTimeView_WhenTasksAreLeftOut_ShouldListThem', async ({ mount, page }) => {
  await open(mount, page);
  await expect(page.getByTestId(ids.leftOut)).toHaveAttribute('open', '');
  await expect(page.getByTestId(ids.leftOutRow)).toHaveCount(VIEW.leftOut.length);
  await expect(page.getByTestId(ids.leftOutRow).first()).toBeVisible();
  await expect(page.getByTestId(ids.leadTimeRule)).toHaveAttribute('open', '');
});

test('LeadTimeView_WhenManyTasksAreLeftOut_ShouldFoldThemBehindTheirCount', async ({
  mount,
  page,
}) => {
  const leftOut = Array.from({ length: 120 }, (_, i) => ({
    ...VIEW.leftOut[0],
    key: `SHIP-${2000 + i}`,
    title: `Follow-up ${i}: ${'a note long enough to wrap over several lines of the page '.repeat(4)}`,
  }));
  await open(mount, page, ROUTE, stubView({ leftOut }));
  const list = page.getByTestId(ids.leftOut);
  await expect(list).not.toHaveAttribute('open');
  await expect(list.locator('summary')).toContainText('Left out: 120 tasks');
  await expect(page.getByTestId(ids.leftOutRow).first()).toBeHidden();

  await list.locator('summary').click();
  await expect(page.getByTestId(ids.leftOutRow)).toHaveCount(120);
  await expect(page.getByTestId(ids.leftOutRow).first()).toBeVisible();
  // Open, it scrolls inside its own panel, and a long title stays on one line.
  expect((await list.boundingBox())!.height).toBeLessThan(400);
  expect((await page.getByTestId(ids.leftOutRow).first().boundingBox())!.height).toBeLessThan(40);
});
