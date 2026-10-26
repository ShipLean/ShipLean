import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardViewDto } from '../../../api/views';
import { componentTestIds as toastIds } from '../../../components/testIds';
import { BoardHarness } from '../ct/BoardHarness';
import { GridHarness } from '../ct/GridHarness';
import { card, cell, lane, storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
type Locator = ReturnType<Page['getByTestId']>;

const route = { hooksConfig: { route: '/p/SHIP/boards/stories' } };

const cardKeyed = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });

// The cells of a lane, in track order: New, Ready (Doing), Ready (Done), In progress, Done.
const cellsOf = (page: Page, laneName: string) =>
  page
    .getByTestId(ids.lane)
    .filter({ has: page.getByTestId(ids.laneName).filter({ hasText: laneName }) })
    .getByTestId(ids.cell);
const NEW = 0;
const READY = 1;
const READY_DONE = 2;
const IN_PROGRESS = 3;

// The card of that key when it sits in the cell: a count of one.
const inCell = (page: Page, laneName: string, track: number, key: string) =>
  cellsOf(page, laneName)
    .nth(track)
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });

const toasts = (page: Page) => page.getByTestId(toastIds.toastHost).getByTestId(toastIds.toast);

// Picks the card up with the pointer and holds it in the middle of the target.
async function dragOver(page: Page, source: Locator, target: Locator) {
  await target.scrollIntoViewIfNeeded();
  const from = (await source.boundingBox())!;
  const to = (await target.boundingBox())!;
  const startX = from.x + from.width / 2;
  const startY = from.y + from.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + 6, startY + 6, { steps: 4 });
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, {
    steps: 12,
  });
}

// Picks the card up with the pointer and puts it down in the middle of the target.
async function drag(page: Page, source: Locator, target: Locator) {
  await dragOver(page, source, target);
  await page.mouse.up();
}

// Resolves once the board reads asked for before it were answered: requests are answered in the order they were made.
async function afterPendingReads(page: Page) {
  await page.route('**/api/sentinel', (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: '{}' }),
  );
  await page.evaluate(() => fetch('/api/sentinel'));
}

const refused = {
  status: 400,
  body: { code: 'validation', error: 'No.' },
};

// Two cards in the default lane's New cell, for reordering.
function boardWithTwoNew(): BoardViewDto {
  const base = storiesBoard();
  return {
    ...base,
    lanes: base.lanes.map((candidate) =>
      candidate.id !== 'lane-default'
        ? candidate
        : lane({
            ...candidate,
            cells: [
              cell('col-new', [card({ key: 'SHIP-163' }), card({ key: 'SHIP-164' })]),
              ...candidate.cells.slice(1),
            ],
          }),
    ),
  };
}

test('BoardMove_WhenDroppedInColumnOfOtherLane_ShouldSendColumnHalfAndLane', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await drag(page, cardKeyed(page, 'SHIP-160'), cellsOf(page, 'Expedite').nth(NEW));
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toEqual({
    itemId: 'id-SHIP-160',
    expectedRevision: 1,
    columnId: 'col-new',
    done: false,
    laneId: 'lane-expedite',
    beforeItemId: null,
  });
});

test('BoardMove_WhenDroppedInDoneHalf_ShouldSendDoneTrueAndTheCardUnderIt', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await drag(page, cardKeyed(page, 'SHIP-160'), cellsOf(page, 'Default lane').nth(READY_DONE));
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    columnId: 'col-ready',
    done: true,
    laneId: 'lane-default',
    beforeItemId: 'id-SHIP-162',
  });
});

test('BoardMove_WhenDroppedOnCard_ShouldSendItAsBefore', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await drag(page, cardKeyed(page, 'SHIP-160'), cardKeyed(page, 'SHIP-150'));
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    columnId: 'col-prog',
    laneId: 'lane-expedite',
    beforeItemId: 'id-SHIP-150',
  });
});

test('BoardMove_WhenDropped_ShouldShowTheCardAtOnceThenToastTheState', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard(), {
    holdMoves: true,
    result: () => ({
      changes: [
        { field: 'state', from: 's-new', to: 's-act', fromLabel: 'New', toLabel: 'Active' },
      ],
    }),
  });
  await mount(<BoardHarness />, route);
  await drag(page, cardKeyed(page, 'SHIP-160'), cellsOf(page, 'Default lane').nth(IN_PROGRESS));
  // The request is still waiting, and the card is already in its new cell.
  await expect.poll(() => server.moves.length).toBe(1);
  await expect(cellsOf(page, 'Default lane').nth(IN_PROGRESS).getByTestId(ids.cardKey)).toHaveText(
    'SHIP-160',
  );
  await expect(toasts(page)).toHaveCount(0);
  server.release();
  await expect(toasts(page)).toContainText('SHIP-160 → In progress (state Active)');
});

test('BoardMove_WhenTheServerPulledTheCard_ShouldToastItOnStories', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard(), { result: () => ({ notices: ['pulled'] }) });
  await mount(<BoardHarness />, route);
  await cardKeyed(page, 'SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect(toasts(page).filter({ hasText: 'SHIP-160 →' })).toHaveCount(1);
  await expect(toasts(page).filter({ hasText: 'Assigned to you' })).toHaveCount(1);
});

test('BoardMove_WhenTheServerPulledTheCardOnFeatures_ShouldNotToastIt', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard(), {
    level: 'features',
    result: () => ({ notices: ['pulled'] }),
  });
  await mount(<BoardHarness level="features" />, {
    hooksConfig: { route: '/p/SHIP/boards/features' },
  });
  await cardKeyed(page, 'SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect(toasts(page).filter({ hasText: 'SHIP-160 →' })).toHaveCount(1);
  await expect(toasts(page).filter({ hasText: 'Assigned to you' })).toHaveCount(0);
});

test('BoardMove_WhenAltArrowsPressedTwice_ShouldSendTheRevisionTheFirstReturned', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, storiesBoard(), { strictRevisions: true, holdMoves: true });
  await mount(<BoardHarness />, route);
  await cardKeyed(page, 'SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await page.keyboard.press('Alt+ArrowRight');
  // Both presses show at once, on the same card; the second request waits for the first.
  await expect(cellsOf(page, 'Default lane').nth(READY_DONE).getByTestId(ids.cardKey)).toHaveText([
    'SHIP-162',
    'SHIP-160',
  ]);
  await expect(cardKeyed(page, 'SHIP-160')).toBeFocused();
  expect(server.moves).toHaveLength(1);
  server.release();
  await expect.poll(() => server.moves.length).toBe(2);
  expect(server.moves.map((move) => [move.columnId, move.done, move.expectedRevision])).toEqual([
    ['col-ready', false, 1],
    ['col-ready', true, 2],
  ]);
  await expect(toasts(page).filter({ hasText: 'SHIP-160 →' })).toHaveCount(2);
  await expect(toasts(page).filter({ hasText: 'went back' })).toHaveCount(0);
});

test('BoardMove_WhenAltUpPressed_ShouldPutTheCardAboveItsNeighbour', async ({ mount, page }) => {
  const server = await stubBoard(page, boardWithTwoNew(), { holdMoves: true });
  await mount(<BoardHarness />, route);
  await cardKeyed(page, 'SHIP-164').focus();
  await page.keyboard.press('Alt+ArrowUp');
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    itemId: 'id-SHIP-164',
    columnId: 'col-new',
    beforeItemId: 'id-SHIP-163',
  });
  const newCell = cellsOf(page, 'Default lane').nth(NEW);
  await expect(newCell.getByTestId(ids.cardKey)).toHaveText(['SHIP-164', 'SHIP-163']);
  await expect(cardKeyed(page, 'SHIP-164')).toBeFocused();
});

test('BoardMove_WhenAltDownPressed_ShouldPutTheCardBelowItsNeighbour', async ({ mount, page }) => {
  const server = await stubBoard(page, boardWithTwoNew(), { holdMoves: true });
  await mount(<BoardHarness />, route);
  await cardKeyed(page, 'SHIP-163').focus();
  await page.keyboard.press('Alt+ArrowDown');
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({ itemId: 'id-SHIP-163', beforeItemId: null });
  const newCell = cellsOf(page, 'Default lane').nth(NEW);
  await expect(newCell.getByTestId(ids.cardKey)).toHaveText(['SHIP-164', 'SHIP-163']);
});

test('BoardMove_WhenAltArrowAtTheEdge_ShouldSendNothing', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await cardKeyed(page, 'SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowLeft');
  await page.keyboard.press('Alt+ArrowUp');
  await page.keyboard.press('Alt+ArrowDown');
  await expect(toasts(page)).toHaveCount(0);
  expect(server.moves).toEqual([]);
});

test('BoardMove_WhenStale_ShouldSnapBackRefetchAndToast', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard(), { strictRevisions: true });
  await mount(<BoardHarness />, route);
  // Someone else moved the card: the server's revision is past the one on screen.
  await page.route('**/api/boards/b-stories/moves', (r) =>
    r.fulfill({
      status: 409,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Changed.', code: 'conflict', current: {}, theirChanges: [] }),
    }),
  );
  const reads = server.reads.length;
  await drag(page, cardKeyed(page, 'SHIP-160'), cellsOf(page, 'Default lane').nth(IN_PROGRESS));
  await expect(toasts(page)).toContainText(
    'SHIP-160 was changed by someone else, so it went back.',
  );
  await expect(cellsOf(page, 'Default lane').nth(NEW).getByTestId(ids.cardKey)).toHaveText(
    'SHIP-160',
  );
  await expect(cellsOf(page, 'Default lane').nth(IN_PROGRESS).getByTestId(ids.card)).toHaveCount(0);
  await expect.poll(() => server.reads.length).toBeGreaterThan(reads);
});

test('BoardMove_WhenTheServerRefusesTheLane_ShouldSnapBackAndSayWhy', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard(), {
    answerMove: () => ({
      status: 400,
      body: { code: 'validation', error: 'That lane cannot take dropped cards.' },
    }),
  });
  await mount(<BoardHarness />, route);
  await drag(page, cardKeyed(page, 'SHIP-160'), cellsOf(page, 'Expedite').nth(NEW));
  await expect(toasts(page)).toContainText(
    'SHIP-160 could not be moved: That lane cannot take dropped cards.',
  );
  await expect(cellsOf(page, 'Default lane').nth(NEW).getByTestId(ids.cardKey)).toHaveText(
    'SHIP-160',
  );
});

test('BoardMove_WhenATeammateMovesACard_ShouldShowItInItsNewCellOnRefetch', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await expect(cardKeyed(page, 'SHIP-160')).toBeVisible();
  const base = storiesBoard();
  server.setBoard({
    ...base,
    lanes: base.lanes.map((candidate) =>
      candidate.id !== 'lane-default'
        ? candidate
        : lane({
            ...candidate,
            cells: [
              cell('col-new'),
              candidate.cells[1]!,
              candidate.cells[2]!,
              cell('col-prog', [
                card({ key: 'SHIP-160', title: 'Biometric sign-in', revision: 2 }),
              ]),
              candidate.cells[4]!,
            ],
          }),
    ),
  });
  await page.getByTestId('harness-refetch').click();
  await expect(cellsOf(page, 'Default lane').nth(IN_PROGRESS).getByTestId(ids.cardKey)).toHaveText(
    'SHIP-160',
  );
  await expect(cellsOf(page, 'Default lane').nth(NEW).getByTestId(ids.card)).toHaveCount(0);
});

const EXPEDITE_DONE = 4;

test('BoardMove_WhenOneOfTwoMovesFails_ShouldPutBackOnlyThatCard', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard(), {
    answerMove: (body) => (body.itemId === 'id-SHIP-160' ? refused : undefined),
  });
  const refuse = server.holdMovesOf('id-SHIP-160');
  const accept = server.holdMovesOf('id-SHIP-150');
  await mount(<BoardHarness />, route);
  await cardKeyed(page, 'SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowRight');
  // A moved card takes the focus back once it lands in its new cell: wait for that, or it can take the focus from the
  // next card.
  await expect(inCell(page, 'Default lane', READY, 'SHIP-160')).toBeFocused();
  await cardKeyed(page, 'SHIP-150').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect(inCell(page, 'Expedite', EXPEDITE_DONE, 'SHIP-150')).toHaveCount(1);
  refuse();
  await expect(toasts(page).filter({ hasText: 'SHIP-160 could not be moved' })).toHaveCount(1);
  // SHIP-150's move is on its way (it went out behind the refused one): its card stays where it was dropped.
  await expect.poll(() => server.moves.length).toBe(2);
  await expect(inCell(page, 'Expedite', EXPEDITE_DONE, 'SHIP-150')).toHaveCount(1);
  await expect(inCell(page, 'Default lane', NEW, 'SHIP-160')).toHaveCount(1);
  accept();
});

test('BoardMove_WhenAnotherCardsMoveFails_ShouldSendTheNextMoveOfACardItsRevision', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, storiesBoard(), {
    holdMoves: true,
    strictRevisions: true,
    answerMove: (body) => (body.itemId === 'id-SHIP-150' ? refused : undefined),
  });
  await mount(<BoardHarness />, route);
  await cardKeyed(page, 'SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowRight');
  // A moved card takes the focus back once it lands in its new cell: wait for that, or it can take the focus from the
  // next card.
  await expect(inCell(page, 'Default lane', READY, 'SHIP-160')).toBeFocused();
  await cardKeyed(page, 'SHIP-150').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect(inCell(page, 'Expedite', EXPEDITE_DONE, 'SHIP-150')).toBeFocused();
  await cardKeyed(page, 'SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowRight');
  server.release();
  await expect.poll(() => server.moves.length).toBe(3);
  await expect(toasts(page).filter({ hasText: 'SHIP-150 could not be moved' })).toHaveCount(1);
  expect(server.moves.map((move) => [move.itemId, move.expectedRevision])).toEqual([
    ['id-SHIP-160', 1],
    ['id-SHIP-150', 1],
    ['id-SHIP-160', 2],
  ]);
  await expect(toasts(page).filter({ hasText: 'went back' })).toHaveCount(0);
});

test('BoardMove_WhenARefetchIsOnItsWay_ShouldNotLetItUndoTheMove', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard(), { holdMoves: true });
  await mount(<BoardHarness />, route);
  await expect(cardKeyed(page, 'SHIP-160')).toBeVisible();
  const resume = server.pauseReads();
  const reads = server.reads.length;
  await page.getByTestId('harness-refetch').click();
  await expect.poll(() => server.reads.length).toBe(reads + 1);
  await cardKeyed(page, 'SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect(inCell(page, 'Default lane', READY, 'SHIP-160')).toHaveCount(1);
  // The refetch started before the move answers with the board as it was.
  resume();
  await afterPendingReads(page);
  await expect(inCell(page, 'Default lane', READY, 'SHIP-160')).toHaveCount(1);
});

test('BoardMove_WhenTheTabIsFocusedAgainMidMove_ShouldNotRefetchOverIt', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, storiesBoard(), { holdMoves: true });
  await mount(<BoardHarness />, route);
  await expect(cardKeyed(page, 'SHIP-160')).toBeVisible();
  const focusTab = () =>
    page.evaluate(() => document.dispatchEvent(new Event('visibilitychange', { bubbles: true })));
  // With no move on its way, coming back to the tab refetches the board.
  const reads = server.reads.length;
  await focusTab();
  await expect.poll(() => server.reads.length).toBe(reads + 1);
  await cardKeyed(page, 'SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect.poll(() => server.moves.length).toBe(1);
  await focusTab();
  await afterPendingReads(page);
  expect(server.reads).toHaveLength(reads + 1);
  await expect(inCell(page, 'Default lane', READY, 'SHIP-160')).toHaveCount(1);
});

test('BoardMove_WhenACardIsCreatedMidMove_ShouldLeaveTheRefetchToTheLastMove', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, storiesBoard(), { holdMoves: true });
  await mount(<BoardHarness />, route);
  await cardKeyed(page, 'SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect.poll(() => server.moves.length).toBe(1);
  const reads = server.reads.length;
  await page.getByTestId(ids.newCardOpen).click();
  await page.getByTestId(ids.newCardTitle).fill('Offline mode');
  await page.getByTestId(ids.newCardTitle).press('Enter');
  await expect.poll(() => server.creates.length).toBe(1);
  await afterPendingReads(page);
  expect(server.reads).toHaveLength(reads);
  server.release();
  await expect.poll(() => server.reads.length).toBeGreaterThan(reads);
});

test('BoardMove_WhenACardIsDraggedOverAnother_ShouldDrawALineAboveIt', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await dragOver(page, cardKeyed(page, 'SHIP-160'), cardKeyed(page, 'SHIP-150'));
  await expect(cardKeyed(page, 'SHIP-150').getByTestId(ids.dropLine)).toHaveCount(1);
  await expect(page.getByTestId(ids.dropLine)).toHaveCount(1);
  await page.mouse.up();
});

test('BoardMove_WhenACardIsDraggedOverACell_ShouldDrawALineAtTheEndOfIt', async ({
  mount,
  page,
}) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await dragOver(page, cardKeyed(page, 'SHIP-160'), cellsOf(page, 'Expedite').nth(NEW));
  await expect(cellsOf(page, 'Expedite').nth(NEW).getByTestId(ids.dropLine)).toHaveCount(1);
  await expect(page.getByTestId(ids.dropLine)).toHaveCount(1);
  await page.mouse.up();
});

test('BoardMove_WhenACardIsFocused_ShouldDescribeAltArrowsNotSpace', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  const description = cardKeyed(page, 'SHIP-160');
  await expect(description).toHaveAccessibleDescription(/Alt/);
  await expect(description).not.toHaveAccessibleDescription(/space/i);
});

test('Grid_WhenATeammateMovedACard_ShouldFlashIt', async ({ mount, page }) => {
  await mount(<GridHarness view={storiesBoard()} flashing={['id-SHIP-160']} />);
  await expect(cardKeyed(page, 'SHIP-160')).toHaveAttribute('data-flashing', 'true');
  await expect(cardKeyed(page, 'SHIP-150')).toHaveAttribute('data-flashing', 'false');
});
