import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';

// The fields block and its pickers on the CT phone project (390×844, SHIP-714 D5/D6, SHIP-805 C4/C5). Sheets are
// portalled to the body, so they are found on the page, not under the mounted form.

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };

const boxOf = async (locator: {
  boundingBox(): Promise<{ x: number; y: number; width: number; height: number } | null>;
}) => (await locator.boundingBox())!;

test('FieldsStrip_WhenTouch_ShouldLayOutTwoColumnTiles', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);
  const fields = screen.getByTestId(ids.fields);
  const tile = (field: string) => fields.locator(`[data-field="${field}"]`);
  await expect(screen.getByTestId(ids.sprint)).toHaveValue('sp24');

  const strip = await boxOf(fields);
  const state = await boxOf(tile('state'));
  const assignee = await boxOf(tile('assignee'));
  const sprint = await boxOf(tile('sprint'));
  const points = await boxOf(tile('points'));
  // State | Assignee, then Sprint | Story points: two columns of half the width each.
  expect(assignee.y).toBe(state.y);
  expect(assignee.x).toBeGreaterThan(state.x + state.width - 2);
  expect(state.width).toBeGreaterThan(strip.width / 2 - 4);
  expect(sprint.y).toBeGreaterThan(state.y);
  expect(points.y).toBe(sprint.y);
  // Priority, Parent and Tags each take a whole row, in that order.
  let above = sprint.y;
  for (const field of ['priority', 'parent', 'tags']) {
    const box = await boxOf(tile(field));
    expect(box.width, field).toBeGreaterThan(strip.width - 2);
    expect(box.y, field).toBeGreaterThan(above);
    above = box.y;
  }
  // The pickers fill their tile and are touch-sized.
  const button = await boxOf(screen.getByTestId(ids.stateButton));
  expect(button.width).toBeGreaterThan(state.width - 32);
  expect(button.height).toBeGreaterThanOrEqual(32);
});

test('AssigneeField_WhenTouch_ShouldOpenSheetWithCloseOnRight', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.assigneeButton).tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Assignee');
  const title = await boxOf(sheet.getByTestId(touchTestIds.sheetTitle));
  const close = await boxOf(sheet.getByTestId(touchTestIds.sheetClose));
  expect(close.x).toBeGreaterThan(title.x);
  const options = sheet.getByTestId(ids.assigneeOption);
  await expect(options).toHaveCount(3);
  for (const option of await options.all()) {
    expect((await boxOf(option)).height).toBeGreaterThanOrEqual(32);
  }

  // Picking a value applies it and closes the sheet.
  await options.filter({ hasText: 'Unassigned' }).tap();

  await expect(sheet).toHaveCount(0);
  await expect(screen.getByTestId(ids.assigneeButton)).toContainText('Unassigned');
});

test('SprintField_WhenTouch_ShouldStayNativeSelect', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);
  const sprint = screen.getByTestId(ids.sprint);
  await expect(sprint).toHaveValue('sp24');

  expect(await sprint.evaluate((el) => el.tagName)).toBe('SELECT');
  expect(await sprint.evaluate((el) => getComputedStyle(el).fontSize)).toBe('16px');
  expect((await boxOf(sprint)).height).toBeGreaterThanOrEqual(32);
  await sprint.selectOption('');

  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(sprint).toHaveValue('');
});

test('CriteriaSection_WhenTouch_ShouldShowRemoveWithoutHover', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);
  const removes = screen.getByTestId(ids.criterionRemove);
  await expect(removes).toHaveCount(2);

  for (const remove of await removes.all()) {
    await expect(remove).toBeVisible();
    expect(await remove.evaluate((el) => getComputedStyle(el).opacity)).toBe('1');
    const box = await boxOf(remove);
    expect(box.width).toBeGreaterThanOrEqual(32);
    expect(box.height).toBeGreaterThanOrEqual(32);
  }
  const input = screen.getByTestId(ids.criterionInput);
  expect((await boxOf(input)).height).toBeGreaterThanOrEqual(32);

  await removes.first().tap();

  await expect(removes).toHaveCount(1);
});

test('ParentField_WhenTouch_ShouldNotFocusSearchUntilTapped', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.parentButton).tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Parent');
  const search = sheet.getByTestId(ids.parentSearch);
  await expect(search).toBeVisible();
  await expect(search).not.toBeFocused();
  await search.tap();
  await expect(search).toBeFocused();
});
