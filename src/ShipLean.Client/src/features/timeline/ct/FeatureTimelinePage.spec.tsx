import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubProjectApi } from '../../../pages/pagesFixtures';
import { pagesTestIds } from '../../pages/testIds';
import { timelineTestIds as ids } from '../testIds';
import { stubFeatureTimeline } from './stubFeatureTimeline';

const path = '/p/SHIP/items/SHIP-99/timeline';

test('FeatureTimelinePage_WhenLoaded_ShouldRenderEveryRegion', async ({ mount, page }) => {
  await stubProjectApi(page);
  await stubFeatureTimeline(page, { fixture: 'finished' });
  await mount(<AppRoutes />, { hooksConfig: { route: path } });

  await expect(page.getByTestId(pagesTestIds.slotFeatureTimeline)).toBeVisible();
  await expect(page.getByTestId(ids.header)).toBeVisible();
  await expect(page.getByTestId(ids.toolbar)).toHaveAttribute('data-group', 'stories');
  await expect(page.getByTestId(ids.toolbar)).toHaveAttribute('data-deps', 'true');
  await expect(page.getByTestId(ids.chart)).toBeVisible();
  await expect(page.getByTestId(ids.chartBar).first()).toBeVisible();
  await expect(page.getByTestId(ids.rail)).toBeVisible();
  await expect(page.getByTestId(ids.json)).toHaveCount(0);
});

test('FeatureTimelinePage_WhenSelectInQuery_ShouldGiveRailAndChartTheSelectedKey', async ({
  mount,
  page,
}) => {
  await stubProjectApi(page);
  await stubFeatureTimeline(page, { fixture: 'finished' });
  await mount(<AppRoutes />, { hooksConfig: { route: `${path}?select=SHIP-192` } });

  await expect(page.getByTestId(ids.chart)).toHaveAttribute('data-selected', 'SHIP-192');
  await expect(page.getByTestId(ids.rail)).toHaveAttribute('data-selected', 'SHIP-192');
});

test('FeatureTimelinePage_WhenBarClicked_ShouldSelectItInChartAndRail', async ({ mount, page }) => {
  await stubProjectApi(page);
  await stubFeatureTimeline(page, { fixture: 'finished' });
  await mount(<AppRoutes />, { hooksConfig: { route: path } });

  await page.getByTestId(ids.chartBar).first().click();
  const key = await page.getByTestId(ids.chartBar).first().getAttribute('data-key');
  await expect(page.getByTestId(ids.rail)).toHaveAttribute('data-selected', key!);
});

test('FeatureTimelinePage_WhenViewJson_ShouldRenderJsonNotChart', async ({ mount, page }) => {
  await stubProjectApi(page);
  await stubFeatureTimeline(page, { fixture: 'finished' });
  await mount(<AppRoutes />, { hooksConfig: { route: `${path}?view=json&zoom=days` } });

  await expect(page.getByTestId(ids.json)).toBeVisible();
  await expect(page.getByTestId(ids.chart)).toHaveCount(0);
  await expect(page.getByTestId(ids.rail)).toHaveAttribute('data-view', 'json');
  await expect(page.getByTestId(ids.toolbar)).toHaveAttribute('data-zoom', 'days');
});

test('FeatureTimelinePage_WhenNotFound_ShouldShowTheNotFoundState', async ({ mount, page }) => {
  await stubProjectApi(page);
  await stubFeatureTimeline(page, { fixture: 'notFound' });
  await mount(<AppRoutes />, { hooksConfig: { route: path } });

  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
  await expect(page.getByTestId(ids.chart)).toHaveCount(0);
});
