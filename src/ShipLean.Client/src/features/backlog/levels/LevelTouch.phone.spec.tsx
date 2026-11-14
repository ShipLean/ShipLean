import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { EPIC_ROWS, FEATURE_ROWS, levelView } from '../ct/levelFixtures';
import { backlogView, ROWS } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { LevelHarness } from '../ct/LevelHarness';
import { PROJECT } from '../ct/fixtures';
import { stubLevel } from '../ct/stubLevels';
import { backlogTestIds as ids } from '../testIds';

// The Features and Epics backlogs on a phone (SHIP-716 D1, D8). Runs in the CT phone project.

const features = { hooksConfig: { route: '/p/SHIP/backlogs/features' } };
const epics = { hooksConfig: { route: '/p/SHIP/backlogs/epics' } };

test('LevelTable_WhenTouch_ShouldRenderRowsNotTable', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await expect(page.getByTestId(ids.touchRowList)).toBeVisible();
  await expect(page.locator('table')).toHaveCount(0);
  await expect(page.getByTestId(ids.columnHeader)).toHaveCount(0);
  const sideways = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
  expect(sideways).toBe(false);
  // The progress line: the bar, "n / m done", and own points with the sum.
  const checkout = page.locator(`[data-testid="${ids.row}"][data-key="SHIP-101"]`);
  await expect(checkout.getByTestId(ids.levelProgressText)).toHaveText('4 / 8 done');
  await expect(checkout.getByTestId(ids.levelPoints)).toContainText('20');
  await expect(checkout.getByTestId(ids.levelPointsSum)).toContainText('Σ26');
});

test('TouchLevelRow_WhenSumMoreThanQuarterOver_ShouldWarn', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  const warned = page
    .locator(`[data-testid="${ids.row}"][data-key="SHIP-101"]`)
    .getByTestId(ids.levelPointsSum);
  await expect(warned).toHaveAttribute('data-warn', 'true');
  await expect(warned).toHaveClass(/text-caution/);
  const calm = page
    .locator(`[data-testid="${ids.row}"][data-key="SHIP-103"]`)
    .getByTestId(ids.levelPointsSum);
  await expect(calm).toHaveAttribute('data-warn', 'false');
  await expect(calm).not.toHaveClass(/text-caution/);
});

test('TouchLevelRow_WhenEpic_ShouldShowFeaturesCount', async ({ mount, page }) => {
  await stubLevel(page, levelView('epics', EPIC_ROWS));
  await mount(<LevelHarness project={PROJECT} level="epics" />, epics);

  await expect(page.getByTestId(ids.row).getByTestId(ids.levelFeatureCount)).toHaveText(
    '2 Features',
  );
});

test('TouchLevelRow_ShouldHaveNoHandle', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await expect(page.getByTestId(ids.touchRowHandle)).toHaveCount(0);
  await expect(page.getByTestId(ids.grip)).toHaveCount(0);
  await expect(page.getByTestId(ids.rank)).toHaveCount(0);
});

for (const [name, level, rows, route] of [
  ['Features', 'features', FEATURE_ROWS, features],
  ['Epics', 'epics', EPIC_ROWS, epics],
] as const) {
  test(`LevelBacklog_When${name}OnPhone_ShouldOfferSearchInMoreAndTheFilterSheet`, async ({
    mount,
    page,
  }) => {
    await stubLevel(page, levelView(level, rows));
    await mount(<LevelHarness project={PROJECT} level={level} />, route);
    await expect(page.getByTestId(ids.row)).toHaveCount(rows.length);

    await page.getByTestId(touchTestIds.moreButton).tap();
    await expect(
      page.locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="search"]`),
    ).toBeVisible();
    await page.keyboard.press('Escape');

    await page.getByTestId(ids.filterButton).tap();
    await expect(page.getByTestId(ids.filterSheet)).toBeVisible();
    await expect(page.getByTestId(ids.filterPanel)).toHaveCount(0);
  });
}

const TOUCH_TARGET = 32;
const heightOf = async (locator: { boundingBox(): Promise<{ height: number } | null> }) =>
  (await locator.boundingBox())!.height;

test('TouchLevelRow_WhenOneFeature_ShouldSaySingular', async ({ mount, page }) => {
  const one = { ...EPIC_ROWS[0], rollups: [{ ...EPIC_ROWS[0].rollups[1], open: 1 }] };
  await stubLevel(page, levelView('epics', [one]));
  await mount(<LevelHarness project={PROJECT} level="epics" />, epics);

  await expect(page.getByTestId(ids.levelFeatureCount)).toHaveText('1 Feature');
});

test('LevelNewItemRow_WhenOpenedOnPhone_ShouldHaveTouchSizedButtons', async ({ mount, page }) => {
  await stubLevel(page, levelView('epics', EPIC_ROWS));
  await mount(<LevelHarness project={PROJECT} level="epics" />, epics);
  await page.getByTestId(ids.newItem).tap();

  await expect(page.getByTestId(ids.addRow)).toBeVisible();
  expect(await heightOf(page.getByTestId(ids.addSubmit))).toBeGreaterThanOrEqual(TOUCH_TARGET);
  expect(await heightOf(page.getByTestId(ids.addClose))).toBeGreaterThanOrEqual(TOUCH_TARGET);
});

test('NewItemRow_WhenNewChosenInStoriesListOnPhone_ShouldShowTheAddRow', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  await expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length);
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);

  await page.getByTestId(ids.newItem).tap();

  await expect(page.getByTestId(ids.addRow)).toBeVisible();
  await expect(page.getByTestId(ids.addTitle)).toHaveCSS('font-size', '16px');
  expect(await heightOf(page.getByTestId(ids.addSubmit))).toBeGreaterThanOrEqual(TOUCH_TARGET);
  expect(await heightOf(page.getByTestId(ids.addClose))).toBeGreaterThanOrEqual(TOUCH_TARGET);
  const sideways = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
  expect(sideways).toBe(false);
});

test('NewFeatureDialog_WhenOpenedOnPhone_ShouldHaveTouchSizedControls', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);
  await page.getByTestId(ids.newItem).tap();

  await expect(page.getByTestId(ids.newFeatureCancel)).toBeVisible();
  expect(await heightOf(page.getByTestId(ids.newFeatureCancel))).toBeGreaterThanOrEqual(
    TOUCH_TARGET,
  );
  expect(await heightOf(page.getByTestId(ids.newFeatureCreate))).toBeGreaterThanOrEqual(
    TOUCH_TARGET,
  );
  const options = page.getByTestId(ids.newFeatureStartOption);
  for (let i = 0; i < (await options.count()); i++) {
    expect(await heightOf(options.nth(i))).toBeGreaterThanOrEqual(TOUCH_TARGET);
  }
});
