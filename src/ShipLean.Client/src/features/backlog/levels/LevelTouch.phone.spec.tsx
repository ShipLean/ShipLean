import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { touchTestIds } from '../../../touch/testIds';
import { EPIC_ROWS, FEATURE_ROWS, levelView } from '../ct/levelFixtures';
import { backlogView, ROWS } from '../ct/fixtures';
import { FEATURE_STATES } from '../newFeature/ct/stubNewFeature';
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

    await page.getByTestId(ids.toolbarRow).getByTestId(touchTestIds.moreButton).tap();
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
  expect(await page.getByTestId(ids.addRow).evaluate((e) => e.tagName)).toBe('DIV');
  await expect(page.locator('tr')).toHaveCount(0);
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
  expect(await page.getByTestId(ids.addRow).evaluate((e) => e.tagName)).toBe('DIV');
  await expect(page.locator('tr')).toHaveCount(0);
  await expect(page.getByTestId(ids.addTitle)).toHaveCSS('font-size', '16px');
  expect(await heightOf(page.getByTestId(ids.addSubmit))).toBeGreaterThanOrEqual(TOUCH_TARGET);
  expect(await heightOf(page.getByTestId(ids.addClose))).toBeGreaterThanOrEqual(TOUCH_TARGET);
  const sideways = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
  expect(sideways).toBe(false);
});

test('LevelNewItemRow_OnPhone_ShouldShowTheHelpLineAndClose', async ({ mount, page }) => {
  await stubLevel(page, levelView('epics', EPIC_ROWS));
  await mount(<LevelHarness project={PROJECT} level="epics" />, epics);
  await page.getByTestId(ids.newItem).tap();

  await expect(page.getByTestId(ids.addHelp)).toContainText('stays open for the next one');
  await expect(page.getByTestId(ids.addClose)).toHaveText('Close');
  await page.getByTestId(ids.addClose).tap();
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);
});

test('InlineAdd_AtTouch_ShouldUseASegmentedSwitchAndClose', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  await page.getByTestId(ids.newItem).tap();

  const switchButtons = page.getByTestId(ids.addRow).getByTestId(ids.addType);
  await expect(switchButtons).toHaveText(['Story', 'Bug']);
  await expect(switchButtons.first()).toHaveAttribute('aria-pressed', 'true');
  await switchButtons.nth(1).tap();
  await expect(switchButtons.nth(1)).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId(ids.addHelp)).toContainText('stays open for the next one');
  await expect(page.getByTestId(ids.addClose)).toHaveText('Close');
  await page.getByTestId(ids.addClose).tap();
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);
});

test('NewFeatureDialog_WhenOpenedOnPhone_ShouldHaveTouchSizedControls', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS), { states: FEATURE_STATES });
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
  await expect(options).toHaveCount(2);
  for (let i = 0; i < (await options.count()); i++) {
    expect(await heightOf(options.nth(i))).toBeGreaterThanOrEqual(TOUCH_TARGET);
  }
});

const rowOf = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.row}"][data-key="${key}"]`);

// Catches: removing `flex-1 min-w-0` from the progress wrapper (the bar collapses to 0px in the touch row).
test('LevelRow_ProgressBar_ShouldBeWiderThanZero', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  const bar = rowOf(page, 'SHIP-101').getByTestId(ids.levelProgressBar);
  const box = await bar.boundingBox();
  expect(box!.width).toBeGreaterThan(40);
});

// Catches: dropping the rank cell or the LevelRowMore button from the touch row.
test('LevelRow_ShouldShowRankAndMore', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  const row = rowOf(page, 'SHIP-101');
  await expect(row.getByTestId(ids.rank)).toHaveText(String(FEATURE_ROWS[0].rank));
  const more = row.getByTestId(touchTestIds.moreButton);
  await expect(more).toHaveAttribute('aria-label', 'More for SHIP-101');
  await more.tap();
  for (const id of ['open', 'top', 'bottom']) {
    await expect(
      page.locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${id}"]`),
    ).toBeVisible();
  }
});

// Catches: removing the `contains` guard from TouchLevelRow's tap (the sheet scrim's tap bubbles to the row and opens the item).
test('LevelRowMore_WhenScrimTapped_ShouldNotOpenTheRow', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  await rowOf(page, 'SHIP-101').getByTestId(touchTestIds.moreButton).tap();
  await page.getByTestId(touchTestIds.sheetScrim).tap({ position: { x: 20, y: 20 } });

  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId('harness-location')).toHaveText('/p/SHIP/backlogs/features');
});

// Catches: sending slot 'last' (or no placement) for Move to top.
test('LevelRowMore_MoveToTop_ShouldReorder', async ({ mount, page }) => {
  const server = await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  await rowOf(page, 'SHIP-103').getByTestId(touchTestIds.moreButton).tap();
  await page.locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="top"]`).tap();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items).toHaveLength(1);
  expect(server.bulks[0].items[0].placement).toEqual({ slot: 'first', beforeItemId: null });
});
