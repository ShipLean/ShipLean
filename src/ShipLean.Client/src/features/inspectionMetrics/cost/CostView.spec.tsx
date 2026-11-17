import { expect, test } from '@playwright/experimental-ct-react';
import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import { AppRoutes } from '../../../AppRoutes';
import { stubProjectApi } from '../../../pages/pagesFixtures';
import {
  routeInspectionMetrics,
  stubCostOff,
  stubCostOn,
  stubCostUnavailable,
} from '../ct/stubInspectionMetrics';
import { inspectionMetricsTestIds as ids } from '../testIds';

const ROUTE = '/p/SHIP/inspection?view=cost';
const VIEW = stubCostOn();
const COST = VIEW.cost!;
const NOT_KEPT = COST.periods.find((p) => !p.stats.kept)!;
const KEPT = COST.periods.find((p) => p.stats.kept && p.stats.tasksWithRuns > 0)!;
const TOP = COST.mostTokens[0];

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
type Mount = Parameters<Parameters<typeof test>[2]>[0]['mount'];

async function open(mount: Mount, page: Page, view: InspectionMetricsViewDto = VIEW) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await stubProjectApi(page);
  const server = await routeInspectionMetrics(page, view);
  await mount(<AppRoutes />, { hooksConfig: { route: ROUTE } });
  return server;
}

/** The fixture with the finished sprint's tokens-per-task label in a shape no client formatter would give its number. */
function oddLabel(): InspectionMetricsViewDto {
  const view = stubCostOn();
  const cost = view.cost!;
  return {
    ...view,
    cost: {
      ...cost,
      headline: {
        ...cost.headline,
        finished: { ...cost.headline.finished!, tokensPerTaskLabel: 'about 154 thousand' },
      },
    },
  };
}

const card = (page: Page, testId: string) =>
  page.getByTestId(testId).getByTestId(ids.costCardValue);

for (const theme of ['light', 'dark'] as const) {
  test.describe(theme, () => {
    test.beforeEach(async ({ page }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    });

    test(`CostView_WhenCostIsOn_ShouldShowTheServersMeanLabel_${theme}`, async ({
      mount,
      page,
    }) => {
      await open(mount, page, oddLabel());
      await expect(card(page, ids.costCardPerTask)).toHaveText('about 154 thousand');
      await expect(card(page, ids.costCardPerMergedPr)).toHaveText(
        COST.headline.finished!.tokensPerMergedPrLabel,
      );
      await expect(card(page, ids.costCardWithRuns)).toHaveText(
        `${COST.headline.finished!.tasksWithRunsPct}%`,
      );
      // The sprint and quarter names come from the lead-time headline.
      await expect(page.getByTestId(ids.costCardPerTask)).toContainText(
        `mean, ${VIEW.headline.finished!.name}`,
      );
      await expect(page.getByTestId(ids.costCardPerTask)).toContainText(
        `${VIEW.headline.quarterToDate!.quarter} to date ${COST.headline.quarterToDate!.tokensPerTaskLabel}`,
      );
    });

    test(`CostView_WhenAPeriodWasNotKept_ShouldShowADashNotZero_${theme}`, async ({
      mount,
      page,
    }) => {
      await open(mount, page);
      const bar = (id: string) =>
        page.getByTestId(ids.costChartBar).and(page.locator(`[data-period-id="${id}"]`));
      await expect(bar(NOT_KEPT.periodId).getByTestId(ids.costNotKept)).toHaveText('—');
      await expect(bar(KEPT.periodId)).toContainText(KEPT.stats.tokensPerTaskLabel);
      await expect(bar(KEPT.periodId).getByTestId(ids.costNotKept)).toHaveCount(0);
      const row = page
        .getByTestId(ids.costPeriodRow)
        .and(page.locator(`[data-period-id="${NOT_KEPT.periodId}"]`));
      await expect(row.getByTestId(ids.costNotKept)).toBeVisible();
      await expect(page.getByTestId(ids.costQuarterRow)).toHaveCount(COST.quarters.length);
    });

    test(`CostView_WhenCostIsUnavailable_ShouldShowTheNoteAndNoNumbers_${theme}`, async ({
      mount,
      page,
    }) => {
      const view = stubCostUnavailable();
      await open(mount, page, view);
      await expect(page.getByTestId(ids.costUnavailable)).toHaveText(view.cost!.note!);
      await expect(page.getByTestId(ids.costCardValue)).toHaveCount(0);
      await expect(page.getByTestId(ids.costChart)).toHaveCount(0);
      await expect(page.getByTestId(ids.costMostTokensRows)).toHaveCount(0);
    });

    test(`CostView_WhenAgentRunsAreOff_ShouldSaySo_${theme}`, async ({ mount, page }) => {
      await open(mount, page, stubCostOff());
      await expect(page.getByTestId(ids.costOff)).toHaveText(
        'Agent runs are off for this project.',
      );
      await expect(page.getByTestId(ids.costCardValue)).toHaveCount(0);
    });
  });
}

test('CostView_WhenNoShareOfTasksWithRuns_ShouldShowADash', async ({ mount, page }) => {
  const view = stubCostOn();
  const cost = view.cost!;
  await open(mount, page, {
    ...view,
    cost: {
      ...cost,
      headline: {
        ...cost.headline,
        finished: { ...cost.headline.finished!, tasksWithRunsPct: null },
      },
    },
  });
  await expect(card(page, ids.costCardWithRuns)).toHaveText('—');
});

test('CostView_WhenMostTokensRowClicked_ShouldOpenTheTaskItemPageAndPostTaskOpened', async ({
  mount,
  page,
}) => {
  const server = await open(mount, page);
  const rows = page.getByTestId(ids.costMostTokensRow);
  await expect(rows).toHaveCount(COST.mostTokens.length);
  await expect(rows.first()).toHaveAttribute('data-key', TOP.key);
  const link = rows.first().getByTestId(ids.costMostTokensLink);
  await expect(link).toHaveAttribute('href', `/p/SHIP/items/${TOP.key}`);
  await link.click();
  await expect(page.getByTestId(ids.cost)).toHaveCount(0);
  await expect
    .poll(() => server.usage.filter((u) => u.path === 'inspection-metrics-task-opened'))
    .toEqual([{ path: 'inspection-metrics-task-opened', body: { from: 'mostTokens' } }]);
});

test('CostView_WhenFeatures_ShouldListEachWithTheServersLabel', async ({ mount, page }) => {
  await open(mount, page);
  const rows = page.getByTestId(ids.costFeatureRow);
  await expect(rows).toHaveCount(COST.byFeature.length);
  await expect(rows.first()).toContainText(COST.byFeature[0].tokensLabel);
});
