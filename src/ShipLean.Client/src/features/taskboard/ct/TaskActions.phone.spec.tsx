import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { TaskboardMoveRequest } from '../taskboardApi';
import { touchTestIds } from '../../../touch/testIds';
import { taskboardTestIds as ids } from '../testIds';
import { AddressProbe } from './AddressProbe';
import { PhoneGridHarness } from './PhoneGridHarness';

// A task's ⋯ sheet on a phone (SHIP-722): move to a state, another story or another person. Runs in the CT phone project.

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100/board' } };
const THEMES = ['light', 'dark'] as const;
const MOVES = '**/api/work-items/f1/feature-board/moves';

// Answers the Feature board's moves endpoint and keeps every body POSTed to it.
async function captureMoves(page: Page): Promise<TaskboardMoveRequest[]> {
  const sent: TaskboardMoveRequest[] = [];
  await page.route(MOVES, async (r) => {
    sent.push(r.request().postDataJSON() as TaskboardMoveRequest);
    await r.fulfill({
      json: { written: { revision: 2 }, parentId: null, offerResolve: false },
    });
  });
  return sent;
}

const card = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .and(page.locator(`[data-key="${key}"]`))
    .first();
const sheet = (page: Page) => page.getByTestId(touchTestIds.sheet);

test('TaskActions_WhenStatePicked_ShouldSendSheetMoveAndClose', async ({ mount, page }) => {
  const sent = await captureMoves(page);
  await mount(<PhoneGridHarness board="feature" />, route);

  await card(page, 'SHIP-173').getByTestId(ids.cardActions).tap();
  await expect(sheet(page)).toBeVisible();
  await expect(sheet(page).getByTestId(ids.actionState)).toHaveCount(3);
  await expect(sheet(page).locator('[aria-current="true"]')).toContainText('New');
  await sheet(page).locator(`[data-testid="${ids.actionState}"][data-state-id="t-active"]`).tap();

  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0]).toMatchObject({
    taskId: 'id-SHIP-173',
    stateId: 't-active',
    row: null,
    method: 'sheet',
  });
  await expect(sheet(page)).toHaveCount(0);
});

test('TaskActions_WhenCardTapped_ShouldOpenItem', async ({ mount, page }) => {
  await mount(
    <>
      <PhoneGridHarness board="feature" />
      <AddressProbe />
    </>,
    route,
  );

  await card(page, 'SHIP-173').getByTestId(ids.cardKey).tap();

  await expect(page.getByTestId(ids.address)).toHaveText(/\?item=SHIP-173$/);
  await expect(sheet(page)).toHaveCount(0);
});

test('TaskActions_WhenStoryPicked_ShouldSendSheetReparentInTheSameState', async ({
  mount,
  page,
}) => {
  const sent = await captureMoves(page);
  await mount(<PhoneGridHarness board="parent" />, route);

  await card(page, 'SHIP-173').getByTestId(ids.cardActions).tap();
  await sheet(page).locator('[data-action-id="story"]').tap();
  const search = sheet(page).getByTestId(ids.storySearch);
  await expect(search).not.toBeFocused();
  await search.fill('SHIP-150');
  await sheet(page).getByTestId(ids.storyOption).tap();

  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0]).toMatchObject({
    stateId: 't-new',
    row: { grouping: 'stories', target: 'id-SHIP-150' },
    method: 'sheet',
  });
});

test('TaskActions_WhenAssigneePicked_ShouldSendSheetMoveToThePeopleRow', async ({
  mount,
  page,
}) => {
  const sent = await captureMoves(page);
  await mount(<PhoneGridHarness board="feature" />, route);

  await card(page, 'SHIP-173').getByTestId(ids.cardActions).tap();
  await sheet(page).locator('[data-action-id="assignee"]').tap();
  await sheet(page)
    .locator(`[data-testid="${ids.assigneeOption}"][data-person-id="u-priya"]`)
    .tap();

  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0]).toMatchObject({
    stateId: 't-new',
    row: { grouping: 'people', target: 'u-priya' },
    method: 'sheet',
  });
});

test('TaskActions_WhenCardSwiped_ShouldNotStartAPointerDrag', async ({ mount, page }) => {
  await mount(<PhoneGridHarness board="feature" />, route);
  const touchAction = await card(page, 'SHIP-173').evaluate(
    (el) => getComputedStyle(el).touchAction,
  );
  expect(touchAction).not.toBe('none');
});

for (const theme of THEMES) {
  test(`TaskActions_WhenSheetsOpen_ShouldMatchTheScreenshots_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<PhoneGridHarness board="parent" />, route);

    await card(page, 'SHIP-173').getByTestId(ids.cardActions).tap();
    await expect(sheet(page)).toBeVisible();
    await expect(sheet(page)).toHaveScreenshot(`task-actions-sheet-${theme}.png`);

    await sheet(page).locator('[data-action-id="story"]').tap();
    await expect(sheet(page).getByTestId(ids.storySearch)).toBeVisible();
    await expect(sheet(page)).toHaveScreenshot(`task-move-to-story-sheet-${theme}.png`);
  });
}
