import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import {
  routeInspectionMetrics,
  stubView,
} from '../features/inspectionMetrics/ct/stubInspectionMetrics';
import { inspectionMetricsTestIds as ids } from '../features/inspectionMetrics/testIds';
import { AppRoutesWithGo } from '../features/inspectionMetrics/ct/AppRoutesWithGo';
import { goTestId } from '../features/inspectionMetrics/ct/goTestId';
import { stubProjectApi } from './pagesFixtures';

const roots = {
  overview: ids.overview,
  leadtime: ids.leadTime,
  period: ids.period,
  cost: ids.cost,
  json: ids.json,
} as const;

for (const [view, root] of Object.entries(roots)) {
  test(`InspectionMetricsPage_WhenViewIs_${view}_ShouldRenderOnlyThatSlot`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await stubProjectApi(page);
    await routeInspectionMetrics(page, stubView());
    await mount(<AppRoutes />, { hooksConfig: { route: `/p/SHIP/inspection?view=${view}` } });

    await expect(page.getByTestId(root)).toBeAttached();
    for (const other of Object.values(roots).filter((id) => id !== root)) {
      await expect(page.getByTestId(other)).toHaveCount(0);
    }
    await expect(page.getByTestId(ids.caveat)).toContainText(stubView().caveat);
  });
}

test('InspectionMetricsPage_WhenPeriodView_ShouldReadThatSprintAndTask', async ({
  mount,
  page,
}) => {
  await stubProjectApi(page);
  const server = await routeInspectionMetrics(page, stubView());
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/p/SHIP/inspection?view=period&range=all&p=s-23&select=SHIP-194' },
  });

  await expect(page.getByTestId(ids.period)).toBeAttached();
  expect(server.reads).toEqual([
    '/api/projects/id-SHIP/inspection-metrics?range=all&cut=none&period=s-23&select=SHIP-194',
  ]);
});

for (const status of [404, 500] as const) {
  test(`InspectionMetricsPage_WhenTheReadAnswers_${status}_ShouldShowTheErrorState`, async ({
    mount,
    page,
  }) => {
    await stubProjectApi(page);
    await routeInspectionMetrics(page, { status });
    await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/inspection' } });

    await expect(page.getByTestId(ids.error)).toBeVisible();
    await expect(page.getByTestId(ids.errorRetry)).toBeVisible();
    await expect(page.getByTestId(ids.header)).toBeVisible();
    await expect(page.getByTestId(ids.overview)).toHaveCount(0);
    await expect(page.getByTestId(ids.loading)).toHaveCount(0);
  });
}

test('InspectionMetricsPage_WhenTheReadIsPending_ShouldShowLoading', async ({ mount, page }) => {
  await stubProjectApi(page);
  await page.route('**/api/projects/*/inspection-metrics*', () => undefined);
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/inspection' } });

  await expect(page.getByTestId(ids.loading)).toBeVisible();
  await expect(page.getByTestId(ids.overview)).toHaveCount(0);
});

test('InspectionMetricsPage_WhenViewChanges_ShouldPostViewedOncePerView', async ({
  mount,
  page,
}) => {
  await stubProjectApi(page);
  const server = await routeInspectionMetrics(page, stubView());
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/inspection' } });
  await expect(page.getByTestId(ids.overview)).toBeAttached();
  await expect.poll(() => server.usage.length).toBe(1);

  await page.locator(`[data-testid="${ids.viewTab}"][data-view="leadtime"]`).click();
  await expect(page.getByTestId(ids.leadTime)).toBeAttached();
  await expect.poll(() => server.usage.length).toBe(2);
  // Opening the view it is already on is no new view.
  await page.locator(`[data-testid="${ids.viewTab}"][data-view="leadtime"]`).click();
  await page.locator(`[data-testid="${ids.viewTab}"][data-view="overview"]`).click();
  await expect(page.getByTestId(ids.overview)).toBeAttached();
  await expect.poll(() => server.usage.length).toBe(3);

  expect(server.usage).toEqual([
    { path: 'inspection-metrics-viewed', body: { view: 'overview' } },
    { path: 'inspection-metrics-viewed', body: { view: 'leadtime' } },
    { path: 'inspection-metrics-viewed', body: { view: 'overview' } },
  ]);
});

test('InspectionMetricsPage_WhenSelectChangesOnAPeriod_ShouldKeepThePeriodViewMounted', async ({
  mount,
  page,
}) => {
  await stubProjectApi(page);
  await routeInspectionMetrics(page, stubView());
  const reads: string[] = [];
  // The first read answers; the one for the new task never does, so only placeholder data can keep the view up.
  await page.route('**/api/projects/*/inspection-metrics*', (route) => {
    const url = new URL(route.request().url());
    reads.push(url.search);
    return reads.length === 1 ? route.fulfill({ json: stubView() }) : undefined;
  });
  await mount(<AppRoutesWithGo to="/p/SHIP/inspection?view=period&p=s-23&select=SHIP-200" />, {
    hooksConfig: { route: '/p/SHIP/inspection?view=period&p=s-23&select=SHIP-194' },
  });
  await expect(page.getByTestId(ids.period)).toBeAttached();

  await page.getByTestId(goTestId).click();
  await expect.poll(() => reads.length).toBe(2);
  expect(reads[1]).toContain('select=SHIP-200');
  await expect(page.getByTestId(ids.period)).toBeAttached();
  await expect(page.getByTestId(ids.loading)).toHaveCount(0);
});

test('InspectionMetricsPage_WhenTheSprintIsNotTheProjects_ShouldOfferLeadTimeAndNoRetry', async ({
  mount,
  page,
}) => {
  await stubProjectApi(page);
  await routeInspectionMetrics(page, { status: 400 });
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/inspection?view=period&p=gone' } });

  await expect(page.getByTestId(ids.error)).toBeVisible();
  await expect(page.getByTestId(ids.errorRetry)).toHaveCount(0);
  await expect(page.getByTestId(ids.periodBack)).toHaveAttribute(
    'href',
    '/p/SHIP/inspection?view=leadtime',
  );
});
