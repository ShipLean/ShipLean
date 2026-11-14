import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { ToastHost } from '../../../components/ToastHost';
import { TaskboardSlot } from '../../../slots/TaskboardSlot';
import { touchTestIds } from '../../../touch/testIds';
import { taskboardTestIds as ids } from '../testIds';
import { PhoneGridHarness } from './PhoneGridHarness';
import { PROJECT, SPRINT, SPRINTS, VIEW } from './fixtures';
import { stubTaskboard } from './stubApi';

// Pick up and No parent on a phone (SHIP-725 D12, D13). Runs in the CT phone project.

const sprintRoute = { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } };
const featureRoute = { hooksConfig: { route: '/p/SHIP/items/SHIP-100/board' } };
const sprintSlot = (
  <>
    <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />
    <ToastHost />
  </>
);
const THEMES = ['light', 'dark'] as const;

const card = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .and(page.locator(`[data-key="${key}"]`))
    .first();
const sheet = (page: Page) => page.getByTestId(touchTestIds.sheet);

test('PickUp_WhenUnassignedNewTaskOnPhone_ShouldShowWithoutHover', async ({ mount, page }) => {
  await stubTaskboard(page, VIEW, { holdLaterReads: true });
  await mount(sprintSlot, sprintRoute);

  const button = card(page, 'SHIP-173').getByTestId(ids.cardPickUp);
  await expect(button).toBeVisible();
  // The button is the 32px hit area; the pill drawn inside it is 28px.
  expect((await button.boundingBox())!.height).toBe(32);
  expect((await button.locator('span').boundingBox())!.height).toBe(28);
  // Assigned and started tasks never offer it.
  await expect(card(page, 'SHIP-172').getByTestId(ids.cardPickUp)).toHaveCount(0);
});

test('PickUp_WhenTappedOnPhone_ShouldSendPickUpMethod', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, { holdLaterReads: true });
  await mount(sprintSlot, sprintRoute);

  await card(page, 'SHIP-173').getByTestId(ids.cardPickUp).tap();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    stateId: 't-active',
    row: { grouping: 'people', target: 'u-priya' },
    method: 'pick-up',
  });
});

test('TaskActions_WhenPickUpOffered_ShouldListItFirst', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, { holdLaterReads: true });
  await mount(sprintSlot, sprintRoute);

  await card(page, 'SHIP-173').getByTestId(ids.cardActions).tap();
  const rows = sheet(page).locator(
    `[data-testid="${ids.actionPickUp}"], [data-testid="${ids.actionState}"], [data-testid="${touchTestIds.actionSheetRow}"]`,
  );
  await expect(rows.first()).toHaveAttribute('data-testid', ids.actionPickUp);
  await rows.first().tap();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({ method: 'pick-up', stateId: 't-active' });
  await expect(sheet(page)).toHaveCount(0);
});

test('TaskActions_WhenTaskAlreadyAssigned_ShouldNotListPickUp', async ({ mount, page }) => {
  await stubTaskboard(page, VIEW, { holdLaterReads: true });
  await mount(sprintSlot, sprintRoute);

  await card(page, 'SHIP-172').getByTestId(ids.cardActions).tap();
  await expect(sheet(page)).toBeVisible();
  await expect(sheet(page).getByTestId(ids.actionPickUp)).toHaveCount(0);
});

test('MoveToStory_WhenOnSprintBoard_ShouldOfferNoParentFirstAndSendNullTarget', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW, { holdLaterReads: true });
  await mount(sprintSlot, sprintRoute);

  await card(page, 'SHIP-173').getByTestId(ids.cardActions).tap();
  await sheet(page).locator('[data-action-id="story"]').tap();
  const options = sheet(page).locator(
    `[data-testid="${ids.storyNoParent}"], [data-testid="${ids.storyOption}"]`,
  );
  await expect(options.first()).toHaveAttribute('data-testid', ids.storyNoParent);
  await options.first().tap();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    stateId: 't-new',
    row: { grouping: 'stories', target: null },
    method: 'sheet',
  });
});

test('MoveToStory_WhenOnFeatureBoard_ShouldNotOfferNoParent', async ({ mount, page }) => {
  await mount(<PhoneGridHarness board="parent" />, featureRoute);

  await card(page, 'SHIP-173').getByTestId(ids.cardActions).tap();
  await sheet(page).locator('[data-action-id="story"]').tap();
  await expect(sheet(page).getByTestId(ids.storySearch)).toBeVisible();
  await expect(sheet(page).getByTestId(ids.storyNoParent)).toHaveCount(0);
});

for (const theme of THEMES) {
  test(`PickUp_WhenShownOnPhone_ShouldMatchTheScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubTaskboard(page, VIEW, { holdLaterReads: true });
    await mount(sprintSlot, sprintRoute);

    const target = card(page, 'SHIP-173');
    await expect(target.getByTestId(ids.cardPickUp)).toBeVisible();
    await expect(target).toHaveScreenshot(`task-pick-up-phone-${theme}.png`);
  });
}
