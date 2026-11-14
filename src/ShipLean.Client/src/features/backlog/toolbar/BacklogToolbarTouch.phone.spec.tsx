import { expect, test } from '@playwright/experimental-ct-react';
import { criteriaTestIds } from '../../criteria/testIds';
import { touchTestIds } from '../../../touch/testIds';
import { backlogView, PROJECT, ROWS } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { ToolbarTouchHarness } from '../ct/ToolbarTouchHarness';
import { ListMode } from '../list/ListMode';
import { backlogTestIds as ids } from '../testIds';

// The backlog toolbar at 900px and below (SHIP-716, L716b). Runs in the CT phone project.

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const actionRow = (id: string) =>
  `[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${id}"]`;

test('BacklogToolbar_WhenTouch_ShouldFitOneRow', async ({ mount, page }) => {
  await mount(<ToolbarTouchHarness />, route);
  const row = page.getByTestId(ids.toolbarRow);
  const rowBox = await row.boundingBox();
  const parts = [
    page.getByTestId(ids.newItem),
    page.getByTestId(ids.modeSwitch),
    page.getByTestId(ids.filterButton),
    page.getByTestId(touchTestIds.moreButton),
  ];
  for (const part of parts) {
    const box = await part.boundingBox();
    // Every control sits inside the row's height: nothing wrapped onto a second line.
    expect(box!.y).toBeGreaterThanOrEqual(rowBox!.y);
    expect(box!.y + box!.height).toBeLessThanOrEqual(rowBox!.y + rowBox!.height + 0.5);
    expect(box!.x + box!.width).toBeLessThanOrEqual(390);
  }
  expect(rowBox!.height).toBeLessThan(60);
  await expect(page.getByTestId(ids.newItem)).toHaveText('+ New');
  expect(await row.evaluate((el) => el.scrollWidth - el.clientWidth)).toBe(0);
});

test('BacklogToolbar_WhenTouch_ShouldHideColumnsAndKeys', async ({ mount, page }) => {
  await mount(<ToolbarTouchHarness />, route);
  await expect(page.getByTestId(ids.columnsButton)).toHaveCount(0);
  await expect(page.getByTestId('harness-keys')).toHaveCount(0);
  await page.getByTestId(touchTestIds.moreButton).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
  await expect(page.getByTestId(ids.columnsButton)).toHaveCount(0);
  await expect(page.getByTestId('harness-keys')).toHaveCount(0);
});

test('MoreSheet_WhenMoreActionsGiven_ShouldListThemAfterOptions', async ({ mount, page }) => {
  await mount(<ToolbarTouchHarness withActions />, route);
  await page.getByTestId(touchTestIds.moreButton).tap();
  const body = page.getByTestId(touchTestIds.sheetBody);
  const order = await body.evaluate(
    (el, selectors) => {
      const at = (selector: string) => {
        const found = el.querySelector(selector);
        return found ? [...el.querySelectorAll('*')].indexOf(found) : -1;
      };
      return selectors.map(at);
    },
    [
      `[data-testid="${ids.moreSearch}"]`,
      `[data-testid="harness-option"]`,
      actionRow('select'),
      actionRow('estimate'),
      `[data-testid="${ids.completedToggle}"]`,
    ],
  );
  expect(order.every((index) => index >= 0)).toBe(true);
  expect([...order].sort((a, b) => a - b)).toEqual(order);

  await page.locator(actionRow('select')).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId('harness-state')).toContainText('select');
});

test('MoreSheet_WhenSorted_ShouldShowTheSortAndClearItFromThere', async ({ mount, page }) => {
  await mount(<ToolbarTouchHarness sorted />, route);
  await page.getByTestId(touchTestIds.moreButton).tap();
  await expect(page.getByTestId(ids.moreSort)).toContainText('Sorted by Points, ascending');
  await page.getByTestId(ids.moreSortClear).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await page.getByTestId(touchTestIds.moreButton).tap();
  await expect(page.getByTestId(ids.moreSort)).toHaveCount(0);
});

test('MoreSheet_WhenSearchChosen_ShouldOpenTheFieldUnderTheToolbar', async ({ mount, page }) => {
  await mount(<ToolbarTouchHarness />, route);
  await expect(page.getByTestId(ids.searchBox)).toHaveCount(0);
  await page.getByTestId(touchTestIds.moreButton).tap();
  await page.getByTestId(ids.moreSearch).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  const field = page.getByTestId(ids.searchBox);
  await expect(field).toBeVisible();
  const toolbarBox = await page.getByTestId(ids.toolbarRow).boundingBox();
  const fieldBox = await field.boundingBox();
  expect(fieldBox!.y).toBeGreaterThanOrEqual(toolbarBox!.y + toolbarBox!.height);
  await expect(field).toHaveCSS('font-size', '16px');
  await field.fill('160');
  await expect(page.getByTestId('harness-state')).toContainText('SHIP-160');
  await expect(page.getByTestId('harness-state')).not.toContainText('SHIP-158');

  await page.getByTestId(ids.searchCancel).tap();
  await expect(page.getByTestId(ids.searchBox)).toHaveCount(0);
  await expect(page.getByTestId('harness-state')).toContainText('SHIP-158,SHIP-160');
});

test('FilterSheet_WhenValueChanged_ShouldApplyWithoutButton', async ({ mount, page }) => {
  await mount(<ToolbarTouchHarness />, route);
  await page.getByTestId(ids.filterButton).tap();
  await expect(page.getByTestId(ids.filterSheet)).toBeVisible();
  await page.getByTestId(criteriaTestIds.add).tap();
  await expect(page.getByTestId(ids.filterButton)).toHaveText('Filter (1)');
  const value = page.getByTestId(criteriaTestIds.value);
  await value.selectOption({ index: 1 });
  // Applied on the change: the criteria hold the chosen value, and no Apply button is offered.
  const applied = JSON.parse((await page.getByTestId('harness-criteria').textContent()) ?? '');
  expect(applied.clauses).toHaveLength(1);
  await expect(
    page.getByTestId(touchTestIds.sheetBody).getByRole('button', { name: /apply/i }),
  ).toHaveCount(0);

  await page.getByTestId(touchTestIds.sheetClose).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  const chip = page.getByTestId(ids.filterChips).getByTestId(ids.filterChip);
  await expect(chip).toHaveCount(1);
  const chipBox = await chip.boundingBox();
  const toolbarBox = await page.getByTestId(ids.toolbarRow).boundingBox();
  expect(chipBox!.y).toBeGreaterThanOrEqual(toolbarBox!.y + toolbarBox!.height);
  expect(chipBox!.height).toBeGreaterThanOrEqual(32);
  await chip.tap();
  await expect(page.getByTestId(ids.filterChip)).toHaveCount(0);
  await expect(page.getByTestId(ids.filterButton)).toHaveText('Filter');
});

test('FilterSheet_WhenClauseAdded_ShouldStackItsControlsAndKeepThemTouchSized', async ({
  mount,
  page,
}) => {
  await mount(<ToolbarTouchHarness />, route);
  await page.getByTestId(ids.filterButton).tap();
  await page.getByTestId(criteriaTestIds.add).tap();
  const field = await page.getByTestId(criteriaTestIds.field).boundingBox();
  const op = await page.getByTestId(criteriaTestIds.op).boundingBox();
  const value = await page.getByTestId(criteriaTestIds.value).boundingBox();
  const remove = await page.getByTestId(criteriaTestIds.remove).boundingBox();
  // Field and operator share a row, the value and ✕ the next one.
  expect(Math.abs(field!.y - op!.y)).toBeLessThan(2);
  expect(value!.y).toBeGreaterThanOrEqual(field!.y + field!.height);
  expect(Math.abs(value!.y - remove!.y)).toBeLessThan(6);
  expect(remove!.width).toBeGreaterThanOrEqual(32);
  expect(remove!.height).toBeGreaterThanOrEqual(32);
  const sheet = await page
    .getByTestId(ids.filterSheet)
    .evaluate((el) => el.scrollWidth - el.clientWidth);
  expect(sheet).toBe(0);
  for (const id of [criteriaTestIds.field, criteriaTestIds.op, criteriaTestIds.value]) {
    await expect(page.getByTestId(id)).toHaveCSS('font-size', '16px');
  }
});

test('BacklogToolbar_WhenFeaturesLevel_ShouldOfferSearchAndShowCompleted', async ({
  mount,
  page,
}) => {
  await mount(<ToolbarTouchHarness level="features" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/features' },
  });
  await expect(page.getByTestId(ids.newItem)).toHaveText('+ New Feature');
  await expect(page.getByTestId(ids.modeOption)).toHaveText(['List', 'Tree']);
  await page.getByTestId(touchTestIds.moreButton).tap();
  await expect(page.getByTestId(ids.moreSearch)).toBeVisible();
  const completed = page.getByTestId(ids.completedToggle);
  await expect(completed).toBeVisible();
  await completed.check();
  await expect(page.getByTestId('harness-state')).toContainText('true|');
});

test('MoreSheet_WhenSearchTypedInListMode_ShouldNarrowTheTouchRows', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length);
  await page.getByTestId(touchTestIds.moreButton).tap();
  await page.getByTestId(ids.moreSearch).tap();
  await page.getByTestId(ids.searchBox).fill(ROWS[0].key);
  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  await expect(page.getByTestId(ids.dragOffBanner)).toBeVisible();
});
