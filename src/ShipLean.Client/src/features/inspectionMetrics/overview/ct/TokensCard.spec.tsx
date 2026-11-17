import { expect, test } from '@playwright/experimental-ct-react';
import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import { projectDto } from '../../../../pages/pagesFixtures';
import {
  routeInspectionMetrics,
  stubCostOff,
  stubCostOn,
  stubCostUnavailable,
} from '../../ct/stubInspectionMetrics';
import { DEFAULT_INSPECTION_QUERY, type InspectionQuery } from '../../inspectionMetricsQuery';
import { inspectionMetricsTestIds as ids } from '../../testIds';
import { OverviewView } from '../OverviewView';

type Mount = Parameters<Parameters<typeof test>[2]>[0]['mount'];

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

test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await routeInspectionMetrics(page);
});

test('TokensCard_WhenCostIsNull_ShouldNotShow', async ({ mount, page }) => {
  await mountOverview(mount, stubCostOff());
  await expect(page.getByTestId(ids.overviewLeadCard)).toBeVisible();
  await expect(page.getByTestId(ids.overviewTokensCard)).toHaveCount(0);
});

test('TokensCard_WhenCostIsOn_ShouldShowTheServersMeanLabelAndChange', async ({ mount, page }) => {
  const view = stubCostOn();
  const cost = view.cost!;
  const finished = { ...cost.headline.finished!, tokensPerTaskLabel: 'about 154 thousand' };
  await mountOverview(mount, {
    ...view,
    cost: { ...cost, headline: { ...cost.headline, finished } },
  });
  await expect(page.getByTestId(ids.overviewTokensCardValue)).toHaveText('about 154 thousand');
  await expect(page.getByTestId(ids.overviewTokensCardChange)).toHaveText(
    `▼ ${Math.abs(finished.changePctVsPrevious!)}%`,
  );
  await expect(page.getByTestId(ids.overviewTokensCard)).toContainText(
    `mean, ${view.headline.finished!.name}`,
  );
});

test('TokensCard_WhenCostIsUnavailable_ShouldShowADashWithTheNote', async ({ mount, page }) => {
  const view = stubCostUnavailable();
  await mountOverview(mount, view);
  await expect(page.getByTestId(ids.overviewTokensCardValue)).toHaveText('—');
  await expect(page.getByTestId(ids.overviewTokensEmpty)).toHaveText(view.cost!.note!);
});

test('TokensCard_WhenClicked_ShouldOpenCost', async ({ mount, page }) => {
  const patches: Partial<InspectionQuery>[] = [];
  await mountOverview(mount, stubCostOn(), patches);
  await page.getByTestId(ids.overviewTokensCard).click();
  await expect.poll(() => patches).toEqual([{ view: 'cost' }]);
});
