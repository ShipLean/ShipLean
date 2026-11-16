import { expect, test } from '@playwright/experimental-ct-react';
import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import { AppRoutes } from '../../../../AppRoutes';
import { projectDto, stubProjectApi } from '../../../../pages/pagesFixtures';
import {
  routeInspectionMetrics,
  stubLowCoverage,
  stubNoFinishedSprint,
  stubNoTasks,
  stubView,
} from '../../ct/stubInspectionMetrics';
import { DEFAULT_INSPECTION_QUERY, type InspectionQuery } from '../../inspectionMetricsQuery';
import { inspectionMetricsTestIds as ids } from '../../testIds';
import { OverviewView } from '../OverviewView';

type Mount = Parameters<Parameters<typeof test>[2]>[0]['mount'];

const VIEW = stubView();
const FINISHED = VIEW.headline.finished!;
const RUNNING = VIEW.periods.find((p) => p.inProgress)!;
const LONGEST = VIEW.longest[0];

const mountOverview = (
  mount: Mount,
  view: InspectionMetricsViewDto,
  patches: Partial<InspectionQuery>[] = [],
) =>
  mount(
    <div style={{ width: 1200 }}>
      <OverviewView
        project={projectDto('SHIP')}
        view={view}
        query={DEFAULT_INSPECTION_QUERY}
        onQueryChange={(patch) => patches.push(patch)}
      />
    </div>,
  );

for (const scheme of ['light', 'dark'] as const) {
  test.describe(scheme, () => {
    test.beforeEach(async ({ page }) => {
      await page.emulateMedia({ colorScheme: scheme, reducedMotion: 'reduce' });
      await page.setViewportSize({ width: 1440, height: 900 });
    });

    test(`OverviewView_WhenASprintIsRunning_ShouldHeadlineTheLastFinishedOne_${scheme}`, async ({
      mount,
      page,
    }) => {
      await routeInspectionMetrics(page);
      await mountOverview(mount, VIEW);

      const card = page.getByTestId(ids.overviewLeadCard);
      await expect(card).toContainText(`mean, ${FINISHED.name}`);
      await expect(card).not.toContainText(`mean, ${RUNNING.name}`);
      await expect(page.getByTestId(ids.overviewLeadCardValue)).toHaveText(
        FINISHED.leadTime.meanLabel!,
      );
      await expect(page.getByTestId(ids.overviewLeadCardSoFar)).toContainText(
        `${RUNNING.name} so far`,
      );
      await expect(page.getByTestId(ids.overviewLeadCardChange)).toContainText(
        `${FINISHED.leadTime.changePctVsPrevious}%`,
      );
      await expect(card).toContainText(VIEW.headline.lastQuarter!.quarter);
    });

    test(`OverviewView_WhenLongestTasks_ShouldListTheTopSix_${scheme}`, async ({ mount, page }) => {
      await routeInspectionMetrics(page);
      const many = stubView({
        longest: Array.from({ length: 8 }, (_, i) => ({ ...LONGEST, key: `SHIP-${900 + i}` })),
      });
      await mountOverview(mount, many);
      await expect(page.getByTestId(ids.overviewLongestRow)).toHaveCount(6);
    });

    test(`OverviewView_WhenCoverageIsLow_ShouldShowTheBanner_${scheme}`, async ({
      mount,
      page,
    }) => {
      await routeInspectionMetrics(page);
      const overview = await mountOverview(mount, stubLowCoverage());
      await expect(page.getByTestId(ids.coverageBanner)).toContainText('4 of 5 tasks');
      await overview.update(
        <div style={{ width: 1200 }}>
          <OverviewView
            project={projectDto('SHIP')}
            view={VIEW}
            query={DEFAULT_INSPECTION_QUERY}
            onQueryChange={() => undefined}
          />
        </div>,
      );
      await expect(page.getByTestId(ids.overviewLeadCard)).toBeVisible();
      await expect(page.getByTestId(ids.coverageBanner)).toHaveCount(0);
    });

    for (const [name, stub] of [
      ['NoFinishedSprint', stubNoFinishedSprint],
      ['NoTasks', stubNoTasks],
    ] as const) {
      test(`OverviewView_When${name}_ShouldShowTheEmptyState_${scheme}`, async ({
        mount,
        page,
      }) => {
        await routeInspectionMetrics(page);
        await mountOverview(mount, stub());
        await expect(page.getByTestId(ids.overview)).toBeVisible();
        await expect(page.getByTestId(ids.empty)).toBeVisible();
        await expect(page.getByTestId(ids.overviewLeadCard)).toHaveCount(0);
        await expect(page.getByTestId(ids.overviewLongest)).toHaveCount(0);
      });
    }

    test(`OverviewView_WhenFinishedSprintHasTasks_ShouldNotShowTheEmptyState_${scheme}`, async ({
      mount,
      page,
    }) => {
      await routeInspectionMetrics(page);
      await mountOverview(mount, VIEW);
      await expect(page.getByTestId(ids.overviewLeadCard)).toBeVisible();
      await expect(page.getByTestId(ids.empty)).toHaveCount(0);
    });
  });
}

test('OverviewView_WhenBarClicked_ShouldOpenThatPeriod', async ({ mount, page }) => {
  await routeInspectionMetrics(page);
  const patches: Partial<InspectionQuery>[] = [];
  await mountOverview(mount, VIEW, patches);
  const target = VIEW.periods[1];
  await page
    .getByTestId(ids.chartBar)
    .and(page.locator(`[data-period-id="${target.id}"]`))
    .click({ position: { x: 10, y: 20 } });
  await expect.poll(() => patches).toEqual([{ view: 'period', period: target.id, select: null }]);
});

test('OverviewView_WhenDotClicked_ShouldOpenItsPeriodWithTheTaskAndPostTaskOpened', async ({
  mount,
  page,
}) => {
  const server = await routeInspectionMetrics(page);
  const patches: Partial<InspectionQuery>[] = [];
  await mountOverview(mount, VIEW, patches);
  const period = VIEW.periods.find((p) => p.dots.some((d) => d.taskKey === LONGEST.key))!;
  await page
    .getByTestId(ids.chartDot)
    .and(page.locator(`[data-key="${LONGEST.key}"]`))
    .click();
  await expect
    .poll(() => patches)
    .toEqual([{ view: 'period', period: period.id, select: LONGEST.key }]);
  await expect
    .poll(() => server.usage)
    .toEqual([{ path: 'inspection-metrics-task-opened', body: { from: 'chart' } }]);
});

test('OverviewView_WhenLongestRowClicked_ShouldNavigateToItsPeriodWithSelect', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await stubProjectApi(page);
  const server = await routeInspectionMetrics(page, VIEW);
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/inspection' } });

  await page
    .getByTestId(ids.overviewLongestRow)
    .and(page.locator(`[data-key="${LONGEST.key}"]`))
    .click();

  await expect(page.getByTestId(ids.period)).toBeAttached();
  await expect
    .poll(() => server.reads.at(-1))
    .toBe(
      `/api/projects/id-SHIP/inspection-metrics?range=last6&cut=none&period=${LONGEST.periodId}&select=${LONGEST.key}`,
    );
  await expect
    .poll(() => server.usage.filter((u) => u.path === 'inspection-metrics-task-opened'))
    .toEqual([{ path: 'inspection-metrics-task-opened', body: { from: 'longest' } }]);
});

for (const [name, stub] of [
  ['Headline', stubView],
  ['NoFinishedSprint', stubNoFinishedSprint],
] as const) {
  test(`OverviewView_WhenJsonForAgentsClicked_${name}_ShouldAskForTheJsonView`, async ({
    mount,
    page,
  }) => {
    await routeInspectionMetrics(page);
    const patches: Partial<InspectionQuery>[] = [];
    await mountOverview(mount, stub(), patches);
    await page.getByTestId(ids.headerJsonLink).click();
    await expect.poll(() => patches).toEqual([{ view: 'json' }]);
  });
}

test('OverviewView_WhenLeadTimeCardClicked_ShouldOpenLeadTime', async ({ mount, page }) => {
  await routeInspectionMetrics(page);
  const patches: Partial<InspectionQuery>[] = [];
  await mountOverview(mount, VIEW, patches);
  await page.getByTestId(ids.overviewLeadCard).click();
  await expect.poll(() => patches).toEqual([{ view: 'leadtime' }]);
});

test('OverviewView_WhenChangeIsUnderThreePercent_ShouldReadSame', async ({ mount, page }) => {
  await routeInspectionMetrics(page);
  const finished = VIEW.headline.finished!;
  await mountOverview(
    mount,
    stubView({
      headline: {
        ...VIEW.headline,
        finished: { ...finished, leadTime: { ...finished.leadTime, changePctVsPrevious: -2 } },
      },
    }),
  );
  await expect(page.getByTestId(ids.overviewLeadCardChange)).toHaveText('≈ same');
});
