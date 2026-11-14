import { expect, test } from '@playwright/experimental-ct-react';
import type { ChildSummaryDto } from '@shiplean/core/api/workItems';
import { touchTestIds } from '../../../../touch/testIds';
import { BoardHarness } from '../../ct/BoardHarness';
import { featuresBoard } from '../../ct/fixtures';
import { stubBoard } from '../../ct/stubApi';
import { boardTestIds as ids } from '../../testIds';

// The Move to Done check as a sheet on a phone (SHIP-719, L719g). Runs in the CT phone project.
// Regenerate shots with scripts/update-page-shots.sh src/features/board/moves/doneConfirm/DoneConfirmTouch.phone.spec.tsx.

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
const route = { hooksConfig: { route: '/p/SHIP/boards/features' } };
const TOUCH = 32;

const child = (key: string, state: string): ChildSummaryDto => ({
  id: `id-${key}`,
  key,
  type: { id: 'story', name: 'User Story' },
  title: `Title of ${key}`,
  state: { id: `s-${state}`, name: state, category: 'proposed' },
  assignee: null,
  points: 3,
});

async function stubChildren(
  page: Page,
  children = [child('SHIP-144', 'Active'), child('SHIP-151', 'New')],
) {
  await page.route('**/api/work-items/*/children', (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(children) }),
  );
}

const sheet = (page: Page) => page.getByTestId(touchTestIds.sheet);

// Asks through Move to on the card's ⋯, the way a phone reaches a move into Done.
async function askForDone(page: Page) {
  await page
    .getByTestId(ids.card)
    .filter({ hasText: 'SHIP-102' })
    .getByTestId(touchTestIds.moreButton)
    .tap();
  await page.locator('[data-action-id="move-to"]').tap();
  await page.locator('[data-action-id^="column:col-done"]').tap();
  await expect(page.getByTestId(ids.doneConfirm)).toBeVisible();
}

test('DoneConfirm_WhenTouch_ShouldBeASheetWithMoveAnywayOnTop', async ({ mount, page }) => {
  await stubBoard(page, featuresBoard(), { level: 'features' });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);
  await askForDone(page);

  await expect(sheet(page).getByTestId(touchTestIds.sheetTitle)).toHaveText('2 stories still open');
  await expect(sheet(page).getByTestId(ids.doneConfirmChild)).toHaveCount(2);
  const move = (await page.getByTestId(ids.doneConfirmMove).boundingBox())!;
  const cancel = (await page.getByTestId(ids.doneConfirmCancel).boundingBox())!;
  const footer = (await page.getByTestId(touchTestIds.sheetFooter).boundingBox())!;
  expect(move.y + move.height).toBeLessThanOrEqual(cancel.y);
  expect(move.width).toBeGreaterThan(footer.width - 30);
  expect(cancel.width).toBeGreaterThan(footer.width - 30);
});

test('DoneConfirm_WhenMoveAnywayTapped_ShouldConfirm', async ({ mount, page }) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features' });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);
  await askForDone(page);
  expect(server.moves).toHaveLength(0);

  await page.getByTestId(ids.doneConfirmMove).tap();

  await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0);
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({ itemId: 'id-SHIP-102', columnId: 'col-done' });
});

test('DoneConfirm_WhenCancelledOrClosed_ShouldNotMove', async ({ mount, page }) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features' });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);

  await askForDone(page);
  await page.getByTestId(ids.doneConfirmCancel).tap();
  await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0);

  await askForDone(page);
  await page.getByTestId(touchTestIds.sheetClose).tap();
  await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0);

  await askForDone(page);
  await page.getByTestId(touchTestIds.sheetScrim).tap({ position: { x: 5, y: 5 } });
  await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0);
  expect(server.moves).toHaveLength(0);
});

test('DoneConfirm_WhenTouch_ShouldHaveChildRowsAtLeast32px', async ({ mount, page }) => {
  await stubBoard(page, featuresBoard(), { level: 'features' });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);
  await askForDone(page);

  for (const row of await sheet(page).getByTestId(ids.doneConfirmChild).all())
    expect((await row.boundingBox())!.height).toBeGreaterThanOrEqual(TOUCH);
  for (const id of [ids.doneConfirmMove, ids.doneConfirmCancel])
    expect((await page.getByTestId(id).boundingBox())!.height).toBeGreaterThanOrEqual(TOUCH);
});

for (const theme of ['light', 'dark'] as const) {
  test(`DoneConfirmPhone_WhenOpened_ShouldMatchItsScreenshot_features-board-done-check_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBoard(page, featuresBoard(), { level: 'features' });
    await stubChildren(page);
    await mount(<BoardHarness level="features" />, route);
    await askForDone(page);
    await expect(sheet(page).getByTestId(ids.doneConfirmChild)).toHaveCount(2);

    await expect(sheet(page)).toHaveScreenshot(`features-board-done-check.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
