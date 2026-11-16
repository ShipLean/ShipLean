import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { componentTestIds } from '../../../components/testIds';
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

test('BeforeYouPlan_WhenTouch_ShouldMakeTheBannersEstimateATouchTarget', async ({
  mount,
  page,
}) => {
  const rows = [1, 2].map((n) => itemRow({ key: `SHIP-${n}`, rank: n, points: null }));
  await stubBacklog(page, { ...backlogView(rows), review: review({ unestimated: [rows[0].id] }) });
  await mount(
    <SelectModeHarness>
      <ListMode project={PROJECT} />
    </SelectModeHarness>,
    list,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(2);

  // At touch the only check is the not-estimated banner (SHIP-1626 D5); its Estimate is the target.
  const estimate = page.getByTestId(ids.notEstimatedEstimate);
  await expect(estimate).toHaveCount(1);
  const box = await estimate.boundingBox();
  expect(box!.height).toBeGreaterThanOrEqual(32);
});

test('BulkSprintSheet_ShouldOfferBacklogAndShowCapacity', async ({ mount, page }) => {
  const server = await stubBacklog(page, planningManyView(), { sprints: planningSprints() });
  await mount(
    <SelectModeHarness>
      <PlanningMode project={PROJECT} />
    </SelectModeHarness>,
    planning,
  );
  await expect(page.getByTestId(ids.row).first()).toBeVisible();
  await pickMore(page, 'select');
  // SHIP-160 is in Sprint 24; SHIP-185 is in the Backlog and has no estimate.
  for (const key of ['SHIP-160', 'SHIP-185']) {
    await rowOf(page, key).getByTestId(ids.touchRowSelect).tap();
    await expect(rowOf(page, key)).toHaveAttribute('data-selected', 'true');
  }

  await page.getByTestId(ids.bulkTouchSprint).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Move 2 items to');
  await expect(sheet).toContainText('SHIP-160, SHIP-185 · 13 pts');
  const sprint24 = page.locator(`[data-testid="${ids.bulkMoveSprint}"][data-sprint="sp24"]`);
  await expect(sprint24.getByTestId(componentTestIds.capacityBar)).toBeVisible();
  await expect(sprint24).toContainText('Current');
  await expect(sprint24.getByTestId(touchTestIds.actionSheetHint)).toHaveText(/^Current · \S+/);
  await expect(page.getByTestId(touchTestIds.actionSheetFooter)).toContainText('has no estimate');
  await expect(page.getByTestId(ids.bulkUnestimatedNote)).toHaveText(
    'SHIP-185 has no estimate, so it adds 0.',
  );

  await page.getByTestId(ids.bulkMoveBacklog).tap();
  await expect.poll(() => server.bulks.length).toBe(1);
  // SHIP-185 is already in the Backlog, so only SHIP-160 moves.
  expect(server.bulks[0].items.map((item) => item.id)).toEqual(['id-SHIP-160']);
  expect(server.bulks[0].items[0].changes?.sprint).toEqual({ value: null });
});

test('BulkSprintSheet_WhenEverySelectedRowIsInTheBacklog_ShouldDisableTheBacklogTarget', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, planningManyView(), { sprints: planningSprints() });
  await mount(
    <SelectModeHarness>
      <PlanningMode project={PROJECT} />
    </SelectModeHarness>,
    planning,
  );
  await expect(page.getByTestId(ids.row).first()).toBeVisible();
  await pickMore(page, 'select');
  // SHIP-185 is in the Backlog; SHIP-160 is in Sprint 24.
  await rowOf(page, 'SHIP-185').getByTestId(ids.touchRowSelect).tap();
  await expect(rowOf(page, 'SHIP-185')).toHaveAttribute('data-selected', 'true');
  await page.getByTestId(ids.bulkTouchSprint).tap();
  await expect(page.getByTestId(ids.bulkMoveBacklog)).toBeDisabled();
  await page.getByTestId(touchTestIds.sheetClose).tap();

  await rowOf(page, 'SHIP-160').getByTestId(ids.touchRowSelect).tap();
  await page.getByTestId(ids.bulkTouchSprint).tap();
  await expect(page.getByTestId(ids.bulkMoveBacklog)).toBeEnabled();
});
