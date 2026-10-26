import { expect, test } from '@playwright/experimental-ct-react';
import type { ChildSummaryDto } from '../../../../api/workItems';
import { BoardHarness } from '../../ct/BoardHarness';
import { featuresBoard } from '../../ct/fixtures';
import { stubBoard } from '../../ct/stubApi';
import { componentTestIds as toastIds } from '../../../../components/testIds';
import { boardTestIds as ids } from '../../testIds';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
type Locator = ReturnType<Page['getByTestId']>;

const route = { hooksConfig: { route: '/p/SHIP/boards/features' } };

const cardKeyed = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });

const child = (
  key: string,
  state: string,
  category: 'proposed' | 'completed',
): ChildSummaryDto => ({
  id: `id-${key}`,
  key,
  type: { id: 'story', name: 'User Story' },
  title: `Title of ${key}`,
  state: { id: `s-${state}`, name: state, category },
  assignee: null,
  points: 3,
});

const CHILDREN = [
  child('SHIP-144', 'Active', 'proposed'),
  child('SHIP-150', 'Closed', 'completed'),
  child('SHIP-151', 'New', 'proposed'),
];

async function stubChildren(page: Page, children = CHILDREN) {
  const asked: string[] = [];
  await page.route('**/api/work-items/*/children', (r) => {
    asked.push(r.request().url());
    return r.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(children),
    });
  });
  return asked;
}

async function drag(page: Page, source: Locator, target: Locator) {
  await target.scrollIntoViewIfNeeded();
  const from = (await source.boundingBox())!;
  const to = (await target.boundingBox())!;
  const startX = from.x + from.width / 2;
  const startY = from.y + from.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + 6, startY + 6, { steps: 4 });
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 12 });
  await page.mouse.up();
}

const doneCell = (page: Page) => page.getByTestId(ids.cell).nth(4);

// dnd-kit swallows any click for 50 ms after a drop, so a click straight after the dialog opens can be lost: click
// again until the dialog is gone (a lost click leaves it standing; a real one closes it).
async function clickPastDropGuard(page: Page, button: Locator) {
  await expect(async () => {
    await button.click({ timeout: 1000 });
    await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0, { timeout: 1000 });
  }).toPass();
}

test('DoneConfirm_WhenCardWithOpenChildrenIsDropped_ShouldListThemAndSendNothingYet', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features' });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);

  await drag(page, cardKeyed(page, 'SHIP-102'), doneCell(page));

  const dialog = page.getByTestId(ids.doneConfirm);
  await expect(dialog).toContainText('still has 2 stories open');
  await expect(dialog).toContainText('from the Stories board');
  // Enter confirms: the primary action has the focus.
  await expect(page.getByTestId(ids.doneConfirmMove)).toBeFocused();
  // Only the open ones are listed: the closed child is not.
  await expect(dialog.getByTestId(ids.doneConfirmChild)).toHaveCount(2);
  await expect(dialog.getByTestId(ids.doneConfirmChild).first()).toContainText('Active');
  expect(server.moves).toHaveLength(0);
});

test('DoneConfirm_WhenCancelled_ShouldSendNothingAndLeaveTheCard', async ({ mount, page }) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features' });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);
  await drag(page, cardKeyed(page, 'SHIP-102'), doneCell(page));

  await clickPastDropGuard(page, page.getByTestId(ids.doneConfirmCancel));

  await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0);
  await expect(page.getByTestId(ids.cell).nth(3).getByTestId(ids.cardKey)).toHaveText(['SHIP-102']);
  expect(server.moves).toHaveLength(0);
});

test('DoneConfirm_WhenMovedAnyway_ShouldSendTheMoveIntoDone', async ({ mount, page }) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features', holdMoves: true });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);
  await drag(page, cardKeyed(page, 'SHIP-102'), doneCell(page));

  await clickPastDropGuard(page, page.getByTestId(ids.doneConfirmMove));

  await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0);
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({ itemId: 'id-SHIP-102', columnId: 'col-done' });
  await expect(doneCell(page).getByTestId(ids.cardKey)).toHaveText(['SHIP-102', 'SHIP-90']);
  server.release();
  await expect(page.getByTestId(toastIds.toastHost).getByTestId(toastIds.toast)).toContainText(
    'SHIP-102 → Done',
  );
  await expect(page.getByTestId(toastIds.toastHost).getByTestId(toastIds.toast)).toContainText(
    '2 stories still open',
  );
});

test('DoneConfirm_WhenAltRightIntoDone_ShouldAskThenMoveOnConfirm', async ({ mount, page }) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features' });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);
  await cardKeyed(page, 'SHIP-102').focus();

  await page.keyboard.press('Alt+ArrowRight');

  await expect(page.getByTestId(ids.doneConfirm)).toBeVisible();
  expect(server.moves).toHaveLength(0);
  await page.getByTestId(ids.doneConfirmMove).click();
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({ itemId: 'id-SHIP-102', columnId: 'col-done' });
});

test('DoneConfirm_WhenEscapePressedOnKeyboardMove_ShouldSendNothing', async ({ mount, page }) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features' });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);
  await cardKeyed(page, 'SHIP-102').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect(page.getByTestId(ids.doneConfirm)).toBeVisible();

  await page.keyboard.press('Escape');

  await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0);
  expect(server.moves).toHaveLength(0);
});

test('DoneConfirm_WhenMoreThanSixAreOpen_ShouldSaySoAfterSix', async ({ mount, page }) => {
  await stubBoard(page, featuresBoard(), { level: 'features' });
  const many = Array.from({ length: 8 }, (_, i) => child(`SHIP-${200 + i}`, 'New', 'proposed'));
  await stubChildren(page, many);
  await mount(<BoardHarness level="features" />, route);

  await drag(page, cardKeyed(page, 'SHIP-102'), doneCell(page));

  await expect(page.getByTestId(ids.doneConfirmChild)).toHaveCount(6);
  await expect(page.getByTestId(ids.doneConfirmMore)).toHaveText('and 2 more');
});

test('DoneConfirm_WhenCardWithAllChildrenClosedIsDropped_ShouldNotAsk', async ({ mount, page }) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features' });
  const asked = await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);

  await drag(page, cardKeyed(page, 'SHIP-104'), doneCell(page));

  await expect.poll(() => server.moves.length).toBe(1);
  await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0);
  expect(asked).toHaveLength(0);
});

test('DoneConfirm_WhenATeammateMovesTheCardIntoDone_ShouldNotAsk', async ({ mount, page }) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features' });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);
  const view = featuresBoard();
  const lane = view.lanes[0]!;
  const moved = lane.cells[3]!.cards[0]!;
  server.setBoard({
    ...view,
    lanes: [
      {
        ...lane,
        cells: lane.cells.map((c, i) =>
          i === 3 ? { ...c, cards: [] } : i === 4 ? { ...c, cards: [moved, ...c.cards] } : c,
        ),
      },
    ],
  });

  await page.getByTestId('harness-refetch').click();

  await expect(doneCell(page).getByTestId(ids.cardKey)).toHaveText(['SHIP-102', 'SHIP-90']);
  await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0);
  expect(server.moves).toHaveLength(0);
});

test('Card_WhenOpenChildrenRemainInDone_ShouldTurnTheRollupWarn', async ({ mount, page }) => {
  await stubBoard(page, featuresBoard(), { level: 'features' });
  await mount(<BoardHarness level="features" />, route);

  await expect(cardKeyed(page, 'SHIP-90').getByTestId(ids.cardRollup)).toHaveAttribute(
    'data-warn',
    'true',
  );
  await expect(cardKeyed(page, 'SHIP-102').getByTestId(ids.cardRollup)).toHaveAttribute(
    'data-warn',
    'false',
  );
});

test('Card_WhenEveryChildIsClosed_ShouldOfferMoveToDoneAsOneMove', async ({ mount, page }) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features', holdMoves: true });
  await stubChildren(page);
  await mount(<BoardHarness level="features" />, route);
  await expect(cardKeyed(page, 'SHIP-102').getByTestId(ids.cardOffer)).toHaveCount(0);
  await expect(cardKeyed(page, 'SHIP-90').getByTestId(ids.cardOffer)).toHaveCount(0);

  await cardKeyed(page, 'SHIP-104').getByTestId(ids.cardMoveToDone).click();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    itemId: 'id-SHIP-104',
    columnId: 'col-done',
    done: false,
    beforeItemId: null,
  });
  await expect(page.getByTestId(ids.doneConfirm)).toHaveCount(0);
  // The card now sits in Done while the move is held: the offer is gone with it.
  await expect(doneCell(page).getByTestId(ids.cardKey)).toContainText(['SHIP-104']);
  await expect(cardKeyed(page, 'SHIP-104').getByTestId(ids.cardOffer)).toHaveCount(0);
});
