import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { LevelHarness } from '../ct/LevelHarness';
import { FEATURE_ROWS, levelView } from '../ct/levelFixtures';
import { PROJECT, backlogView, itemRow, planningView, treeRows, treeView } from '../ct/fixtures';
import { stubLevel } from '../ct/stubLevels';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { PlanningMode } from '../planning/PlanningMode';
import { TreeMode } from '../tree/TreeMode';
import { backlogTestIds as ids } from '../testIds';

const ROWS = [
  itemRow({ key: 'SHIP-1', rank: 1, points: 8 }),
  itemRow({ key: 'SHIP-2', rank: 2, points: null }),
  itemRow({ key: 'SHIP-3', rank: 3, points: 2 }),
];

const rowKeys = (page: Page) =>
  page.getByTestId(ids.row).evaluateAll((els) => els.map((e) => e.getAttribute('data-key')));
const header = (page: Page, column: string) =>
  page.locator(`[data-testid="${ids.columnHeader}"][data-column="${column}"]`);

// The usage reports the screen sends, as their bodies.
async function recordSorts(page: Page) {
  const sorts: unknown[] = [];
  await page.route('**/api/projects/p1/usage/backlog-sort', (route) => {
    sorts.push(route.request().postDataJSON());
    return route.fulfill({ status: 204 });
  });
  return sorts;
}

test('ColumnSort_WhenListTitleClicked_ShouldSortUpThenDownThenBackToRank', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, backlogView(ROWS));
  const sorts = await recordSorts(page);
  await mount(<ListMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  const points = header(page, 'points');

  await points.getByTestId(ids.columnSort).click();
  await expect(points).toHaveAttribute('aria-sort', 'ascending');
  expect(await rowKeys(page)).toEqual(['SHIP-3', 'SHIP-1', 'SHIP-2']);

  await points.getByTestId(ids.columnSort).click();
  await expect(points).toHaveAttribute('aria-sort', 'descending');
  expect(await rowKeys(page)).toEqual(['SHIP-1', 'SHIP-3', 'SHIP-2']);

  await points.getByTestId(ids.columnSort).click();
  await expect(points).not.toHaveAttribute('aria-sort');
  expect(await rowKeys(page)).toEqual(['SHIP-1', 'SHIP-2', 'SHIP-3']);

  await expect.poll(() => sorts.length).toBe(3);
  expect(sorts).toEqual([
    { screen: 'storiesBacklog', column: 'points', direction: 'asc' },
    { screen: 'storiesBacklog', column: 'points', direction: 'desc' },
    { screen: 'storiesBacklog', column: 'points', direction: 'off' },
  ]);
});

test('ColumnSort_WhenListSorted_ShouldTurnReorderingOffUntilRankOrderIsBack', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, backlogView(ROWS));
  await recordSorts(page);
  await mount(<ListMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  await expect(page.getByTestId(ids.grip).first()).toBeEnabled();
  const title = header(page, 'title').getByTestId(ids.columnSort);

  await title.click();
  await expect(page.getByTestId(ids.dragOffBanner)).toContainText('sorted by a column');
  await expect(page.getByTestId(ids.grip).first()).toBeDisabled();

  await title.click();
  await title.click();
  await expect(page.getByTestId(ids.dragOffBanner)).toHaveCount(0);
  await expect(page.getByTestId(ids.grip).first()).toBeEnabled();
});

test('ColumnSort_WhenRankClicked_ShouldSortDescendingFirst', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await recordSorts(page);
  await mount(<ListMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  await expect(page.getByTestId(ids.row)).toHaveCount(3);

  await header(page, 'rank').getByTestId(ids.columnSort).click();
  expect(await rowKeys(page)).toEqual(['SHIP-3', 'SHIP-2', 'SHIP-1']);
});

test('ColumnSort_WhenFeaturesTitleClicked_ShouldSortTheLevel', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  const sorts = await recordSorts(page);
  await mount(<LevelHarness project={PROJECT} level="features" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/features' },
  });
  await expect(page.getByTestId(ids.row)).toHaveCount(3);

  await header(page, 'points').getByTestId(ids.columnSort).click();
  expect(await rowKeys(page)).toEqual(['SHIP-103', 'SHIP-101', 'SHIP-108']);
  await expect.poll(() => sorts.length).toBe(1);
  expect(sorts[0]).toEqual({
    screen: 'featuresBacklog',
    column: 'points',
    direction: 'asc',
  });
});

test('ColumnSort_WhenTreeMode_ShouldOfferNoSorting', async ({ mount, page }) => {
  const { rows } = treeRows();
  await stubBacklog(page, treeView(rows));
  await mount(<TreeMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=tree' },
  });
  await expect(page.getByTestId(ids.columnHeader).first()).toBeVisible();
  await expect(page.getByTestId(ids.columnSort)).toHaveCount(0);
});

test('ColumnSort_WhenPlanningMode_ShouldOfferNoSorting', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  await expect(page.getByTestId(ids.planning)).toBeVisible();
  await expect(page.getByTestId(ids.columnSort)).toHaveCount(0);
});
