import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { touchTestIds } from '../../../touch/testIds';
import {
  backlogView,
  itemRow,
  PROJECT,
  planningManyView,
  planningSprints,
  review,
} from '../ct/fixtures';
import { SelectModeHarness } from '../ct/SelectModeHarness';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { PlanningMode } from '../planning/PlanningMode';
import { backlogTestIds as ids } from '../testIds';

// The Select mode bars on a phone (SHIP-716 D3): the select bar, the bulk bar and its sheets. Runs in the CT phone project.

const planning = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };
const list = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const pickMore = async (page: Page, action: string) => {
  await page.getByTestId(touchTestIds.moreButton).first().tap();
  await page
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${action}"]`)
    .tap();
};
const rowOf = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.row}"][data-key="${key}"]`);
const selectTwo = async (page: Page) => {
  await pickMore(page, 'select');
  for (const key of ['SHIP-180', 'SHIP-181']) {
    await rowOf(page, key).getByTestId(ids.touchRowSelect).tap();
    await expect(rowOf(page, key)).toHaveAttribute('data-selected', 'true');
  }
};

test('BulkBar_WhenTouch_ShouldFixToBottomWithCountAndSum', async ({ mount, page }) => {
  await stubBacklog(page, planningManyView(), { sprints: planningSprints() });
  await mount(
    <SelectModeHarness>
      <PlanningMode project={PROJECT} />
    </SelectModeHarness>,
    planning,
  );
  await expect(page.getByTestId(ids.row).first()).toBeVisible();
  await selectTwo(page);

  const bar = page.getByTestId(ids.bulkBar);
  await expect(page.getByTestId(ids.bulkCount)).toHaveText('2 selected');
  await expect(bar).toContainText('8 pts');
  await expect(bar).toHaveCSS('position', 'fixed');
  const box = await bar.boundingBox();
  const viewport = page.viewportSize();
  expect(box?.y).toBeGreaterThan(0);
  expect((box?.y ?? 0) + (box?.height ?? 0)).toBe(viewport?.height);
  // The bar is the K5 pieces: State, Points, Sprint and a ⋯, no popover.
  await expect(bar.getByTestId(ids.bulkTouchState)).toBeVisible();
  await expect(bar.getByTestId(ids.bulkTouchPoints)).toBeVisible();
  await expect(bar.getByTestId(ids.bulkTouchSprint)).toBeVisible();
  await expect(bar.getByTestId(touchTestIds.moreButton)).toBeVisible();
});

test('BulkSprintSheet_WhenSelectionAdded_ShouldShowEachSprintTotal', async ({ mount, page }) => {
  await stubBacklog(page, planningManyView(), { sprints: planningSprints() });
  await mount(
    <SelectModeHarness>
      <PlanningMode project={PROJECT} />
    </SelectModeHarness>,
    planning,
  );
  await expect(page.getByTestId(ids.row).first()).toBeVisible();
  await selectTwo(page);

  await page.getByTestId(ids.bulkTouchSprint).tap();

  const sprint = (id: string) =>
    page.locator(`[data-testid="${ids.bulkMoveSprint}"][data-sprint="${id}"]`);
  await expect(sprint('sp24').getByTestId(ids.bulkSum)).toHaveText('13 + 8 = 21 / 23');
  await expect(sprint('sp25').getByTestId(ids.bulkSum)).toHaveText('0 + 8 = 8 / 21');
});

test('BulkSprintSheet_WhenSprintTapped_ShouldMoveSelection', async ({ mount, page }) => {
  const server = await stubBacklog(page, planningManyView(), { sprints: planningSprints() });
  await mount(
    <SelectModeHarness>
      <PlanningMode project={PROJECT} />
    </SelectModeHarness>,
    planning,
  );
  await expect(page.getByTestId(ids.row).first()).toBeVisible();
  await selectTwo(page);

  await page.getByTestId(ids.bulkTouchSprint).tap();
  await page.locator(`[data-testid="${ids.bulkMoveSprint}"][data-sprint="sp25"]`).tap();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((item) => item.id)).toEqual(['id-SHIP-180', 'id-SHIP-181']);
  expect(server.bulks[0].items[0].changes?.sprint).toEqual({ value: 'sp25' });
});

test('SelectBar_WhenSelectAllTapped_ShouldSelectEveryRow', async ({ mount, page }) => {
  const rows = [1, 2, 3].map((n) => itemRow({ key: `SHIP-${n}`, rank: n, points: n }));
  await stubBacklog(page, backlogView(rows));
  await mount(
    <SelectModeHarness>
      <ListMode project={PROJECT} />
    </SelectModeHarness>,
    list,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await pickMore(page, 'select');
  await expect(page.getByTestId(ids.selectBarCount)).toHaveText('Select rows');

  await page.getByTestId(ids.selectBarAll).tap();

  await expect(page.getByTestId(ids.selectBarCount)).toHaveText('3 selected');
  await expect(page.getByTestId(ids.touchRowSelect)).toHaveCount(3);
  for (const box of await page.getByTestId(ids.touchRowSelect).all())
    await expect(box).toBeChecked();
  await expect(page.getByTestId(ids.bulkCount)).toHaveText('3 selected');
});

test('SelectBar_WhenDoneTapped_ShouldLeaveSelectModeAndClear', async ({ mount, page }) => {
  const rows = [1, 2].map((n) => itemRow({ key: `SHIP-${n}`, rank: n, points: n }));
  await stubBacklog(page, backlogView(rows));
  await mount(
    <SelectModeHarness>
      <ListMode project={PROJECT} />
    </SelectModeHarness>,
    list,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  await pickMore(page, 'select');
  await page.getByTestId(ids.selectBarAll).tap();
  await expect(page.getByTestId(ids.bulkBar)).toBeVisible();

  await page.getByTestId(ids.selectBarDone).tap();

  await expect(page.getByTestId(ids.selectBar)).toHaveCount(0);
  await expect(page.getByTestId(ids.touchRowSelect)).toHaveCount(0);
  await expect(page.getByTestId(ids.bulkBar)).toHaveCount(0);
});

test('MoreSheet_WhenEstimateUnpointedTapped_ShouldOpenUnestimatedQueue', async ({
  mount,
  page,
}) => {
  const rows = [1, 2, 3].map((n) =>
    itemRow({ key: `SHIP-${n}`, rank: n, points: n === 2 ? null : 5 }),
  );
  await stubBacklog(page, { ...backlogView(rows), review: review({ unestimated: [rows[1].id] }) });
  await mount(
    <SelectModeHarness>
      <ListMode project={PROJECT} />
    </SelectModeHarness>,
    list,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(3);

  await pickMore(page, 'estimate-unpointed');

  await expect(page.getByTestId(ids.pointsSheet)).toBeVisible();
  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-2');
});

test('MoreSheet_WhenNothingUnestimated_ShouldNotOfferEstimate', async ({ mount, page }) => {
  const rows = [1, 2].map((n) => itemRow({ key: `SHIP-${n}`, rank: n, points: 5 }));
  await stubBacklog(page, { ...backlogView(rows), review: review() });
  await mount(
    <SelectModeHarness>
      <ListMode project={PROJECT} />
    </SelectModeHarness>,
    list,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(2);

  await page.getByTestId(touchTestIds.moreButton).first().tap();

  await expect(
    page.locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="select"]`),
  ).toBeVisible();
  await expect(
    page.locator(
      `[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="estimate-unpointed"]`,
    ),
  ).toHaveCount(0);
});

test('PlanningRows_WhenSelecting_ShouldShowBoxesInPlaceOfHandles', async ({ mount, page }) => {
  await stubBacklog(page, planningManyView(), { sprints: planningSprints() });
  await mount(
    <SelectModeHarness>
      <PlanningMode project={PROJECT} />
    </SelectModeHarness>,
    planning,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(12);
  await expect(page.getByTestId(ids.touchHandle)).toHaveCount(12);

  await pickMore(page, 'select');

  await expect(page.getByTestId(ids.touchRowSelect)).toHaveCount(12);
  await expect(page.getByTestId(ids.touchHandle)).toHaveCount(0);
});

test('BeforeYouPlan_WhenTouch_ShouldMakeEachCheckATouchTarget', async ({ mount, page }) => {
  const rows = [1, 2].map((n) => itemRow({ key: `SHIP-${n}`, rank: n, points: null }));
  await stubBacklog(page, { ...backlogView(rows), review: review({ unestimated: [rows[0].id] }) });
  await mount(
    <SelectModeHarness>
      <ListMode project={PROJECT} />
    </SelectModeHarness>,
    list,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(2);

  for (const chip of await page.getByTestId(ids.planCheck).all()) {
    const box = await chip.boundingBox();
    expect(box?.height).toBeGreaterThanOrEqual(32);
  }
});
