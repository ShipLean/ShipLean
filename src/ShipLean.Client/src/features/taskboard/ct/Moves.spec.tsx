import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';
import { TaskboardSlot } from '../../../slots/TaskboardSlot';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import type { CardDto, TaskboardViewDto } from '../../../api/views';
import { taskboardTestIds as ids } from '../testIds';
import { PRIYA, PROJECT, SPRINT, SPRINTS, VIEW } from './fixtures';
import { componentTestIds as c } from '../../../components/testIds';
import { stubTaskboard } from './stubApi';

const route = { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } };
const slot = (
  <>
    <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />
    <ToastHost />
  </>
);

const card = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });
const cell = (page: Page, row: number, column: number) =>
  page.getByTestId(ids.row).nth(row).getByTestId(ids.cell).nth(column);

// Picks the card up with the pointer and puts it down on the middle of a card, or the bottom edge of a cell.
// The target is brought into view and both ends are measured only once they are drawn: the last row (Unparented) can sit
// below the fold, and measuring while the grid is still settling drops the card on the wrong cell.
async function drag(page: Page, from: Locator, to: Locator, where: 'card' | 'cell') {
  await to.scrollIntoViewIfNeeded();
  await expect(from).toBeVisible();
  await expect(to).toBeVisible();
  const a = (await from.boundingBox())!;
  const b = (await to.boundingBox())!;
  const startX = a.x + a.width / 2;
  const startY = a.y + a.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + 6, startY + 6, { steps: 4 });
  await page.mouse.move(
    b.x + b.width / 2,
    where === 'card' ? b.y + b.height / 2 : b.y + b.height - 8,
    {
      steps: 12,
    },
  );
  await page.mouse.up();
}

const hold = { holdLaterReads: true };

const withResolved = (): TaskboardViewDto => ({
  ...VIEW,
  parentStates: [
    {
      type: 'story',
      states: [
        ...VIEW.parentStates[0].states,
        { id: 'ps-resolved', name: 'Resolved', category: 'resolved' },
      ],
    },
  ],
});

test('Taskboard_WhenCardDroppedInColumn_ShouldPostTheStateAndShowItAtOnce', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW, hold);
  await mount(slot, route);

  await drag(page, card(page, 'SHIP-173'), cell(page, 0, 1), 'cell');

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toEqual({
    taskId: 'id-SHIP-173',
    expectedRevision: 1,
    stateId: 't-active',
    row: null,
    beforeItemId: null,
  });
  await expect(cell(page, 0, 1).getByTestId(ids.card)).toHaveCount(2);
  await expect(cell(page, 0, 0).getByTestId(ids.card)).toHaveCount(0);
});

test('Taskboard_WhenCardDroppedAboveCard_ShouldPostTheCardItGoesAbove', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, hold);
  await mount(slot, route);

  await drag(page, card(page, 'SHIP-173'), card(page, 'SHIP-172'), 'card');

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    stateId: 't-active',
    row: null,
    beforeItemId: 'id-SHIP-172',
  });
  await expect(cell(page, 0, 1).getByTestId(ids.cardKey)).toHaveText(['SHIP-173', 'SHIP-172']);
});

test('Taskboard_WhenCardDroppedOnOtherStoryRow_ShouldPostTheParent', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, hold);
  await mount(slot, route);

  await drag(page, card(page, 'SHIP-182'), cell(page, 0, 0), 'cell');
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    taskId: 'id-SHIP-182',
    stateId: 't-new',
    row: { grouping: 'stories', target: 'id-SHIP-141' },
  });
  await expect(page.getByTestId(ids.row).nth(0).getByTestId(ids.card)).toHaveCount(4);
});

test('Taskboard_WhenCardDroppedOnUnparentedRow_ShouldPostANullTarget', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, hold);
  await mount(slot, route);

  await drag(page, card(page, 'SHIP-173'), cell(page, 2, 0), 'cell');
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({ row: { grouping: 'stories', target: null } });
});

test('Taskboard_WhenCardDroppedOnCollapsedRow_ShouldOpenItsRowOnLanding', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, VIEW, hold);
  await mount(slot, route);
  const done = page.getByTestId(ids.row).nth(1);
  await expect(done).toHaveAttribute('data-collapsed', 'true');

  await card(page, 'SHIP-173').focus();
  await page.keyboard.press('Alt+ArrowDown');

  await expect(done).toHaveAttribute('data-collapsed', 'false');
  await expect(done.getByTestId(ids.card)).toHaveCount(2);
});

test('Taskboard_WhenCardDroppedOnPersonRow_ShouldPostThePersonAndNoOrder', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW, { ...hold, saved: { groupBy: 'people' } });
  await mount(slot, route);

  // People rows: Unassigned, Priya, Tom.
  await drag(page, card(page, 'SHIP-173'), cell(page, 1, 1), 'cell');
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toEqual({
    taskId: 'id-SHIP-173',
    expectedRevision: 1,
    stateId: 't-active',
    row: { grouping: 'people', target: 'u-priya' },
    beforeItemId: null,
  });

  // The first drop has been drawn (the layout has stopped moving) before the second drag measures its cells.
  await expect(cell(page, 1, 1).getByTestId(ids.cardKey)).toHaveText(['SHIP-173']);
  await drag(page, card(page, 'SHIP-172'), cell(page, 0, 1), 'cell');
  await expect.poll(() => server.moves.length).toBe(2);
  expect(server.moves[1]).toMatchObject({
    stateId: 't-active',
    row: { grouping: 'people', target: null },
  });
});

test('Taskboard_WhenAltArrowPressed_ShouldMoveTheFocusedCardAndKeepFocus', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW, hold);
  await mount(slot, route);

  await card(page, 'SHIP-172').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({ taskId: 'id-SHIP-172', stateId: 't-closed', row: null });
  await expect(card(page, 'SHIP-172')).toBeFocused();

  await page.keyboard.press('Alt+ArrowLeft');
  await expect.poll(() => server.moves.length).toBe(2);
  expect(server.moves[1]).toMatchObject({ stateId: 't-active' });
  await expect(card(page, 'SHIP-172')).toBeFocused();

  await page.keyboard.press('Alt+ArrowDown');
  await expect.poll(() => server.moves.length).toBe(3);
  expect(server.moves[2]).toMatchObject({ row: { grouping: 'stories', target: 'id-SHIP-138' } });
});

test('Taskboard_WhenAltArrowAtAnEdge_ShouldNotMove', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, hold);
  await mount(slot, route);

  await card(page, 'SHIP-173').focus();
  await page.keyboard.press('Alt+ArrowLeft');
  await page.keyboard.press('Alt+ArrowUp');
  await page.keyboard.press('ArrowRight');
  await expect(card(page, 'SHIP-173')).toBeFocused();
  expect(server.moves).toHaveLength(0);
});

test('Taskboard_WhenPickedUp_ShouldPostMeAndTheFirstInProgressState', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, hold);
  await mount(slot, route);

  // Only an unassigned card in a Proposed state offers it.
  await expect(card(page, 'SHIP-172').getByTestId(ids.cardPickUp)).toHaveCount(0);
  await expect(card(page, 'SHIP-171').getByTestId(ids.cardPickUp)).toHaveCount(0);
  await card(page, 'SHIP-173').hover();
  await card(page, 'SHIP-173').getByTestId(ids.cardPickUp).click();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toEqual({
    taskId: 'id-SHIP-173',
    expectedRevision: 1,
    stateId: 't-active',
    row: { grouping: 'people', target: 'u-priya' },
    beforeItemId: null,
  });
  await expect(cell(page, 0, 1).getByTestId(ids.card)).toHaveCount(2);
});

test('Taskboard_WhenPickedUpInPeopleRows_ShouldMoveToMyRow', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, { ...hold, saved: { groupBy: 'people' } });
  await mount(slot, route);

  await card(page, 'SHIP-173').hover();
  await card(page, 'SHIP-173').getByTestId(ids.cardPickUp).click();

  await expect.poll(() => server.moves.length).toBe(1);
  await expect(page.getByTestId(ids.row).nth(1).getByTestId(ids.card)).toHaveCount(2);
});

test('Taskboard_WhenCardIsAssignedOrPastProposed_ShouldNotOfferPickUp', async ({ mount, page }) => {
  const assignedNew = (card: CardDto) =>
    card.key === 'SHIP-173' ? { ...card, assignee: PRIYA } : card;
  const unassignedActive = (card: CardDto) =>
    card.key === 'SHIP-172' ? { ...card, assignee: null } : card;
  const reshape = (tasks: CardDto[]) => tasks.map(assignedNew).map(unassignedActive);
  const view: TaskboardViewDto = {
    ...VIEW,
    rows: VIEW.rows.map((row) => ({
      ...row,
      cells: row.cells.map((c) => ({ ...c, tasks: reshape(c.tasks) })),
    })),
  };
  await stubTaskboard(page, view);
  await mount(slot, route);

  await expect(card(page, 'SHIP-173')).toBeVisible();
  await expect(card(page, 'SHIP-173').getByTestId(ids.cardPickUp)).toHaveCount(0);
  await expect(card(page, 'SHIP-172').getByTestId(ids.cardPickUp)).toHaveCount(0);
  await expect(card(page, 'SHIP-182').getByTestId(ids.cardPickUp)).toHaveCount(1);
});

const withOffer = (): TaskboardViewDto => ({
  ...withResolved(),
  rows: VIEW.rows.map((row, index) => (index === 0 ? { ...row, offerResolve: true } : row)),
});

test('Taskboard_WhenRowFlagged_ShouldOfferMoveToResolvedAndPatchTheStory', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, withOffer());
  await mount(slot, route);

  await expect(page.getByTestId(ids.rowOffer)).toHaveCount(1);
  await page.getByTestId(ids.rowOfferResolve).click();

  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0]).toMatchObject({
    expectedRevision: 1,
    changes: { state: { value: 'ps-resolved' } },
  });
});

test('Taskboard_WhenRowNotFlagged_ShouldNotOfferMoveToResolved', async ({ mount, page }) => {
  await stubTaskboard(page, VIEW);
  await mount(slot, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await expect(page.getByTestId(ids.rowOffer)).toHaveCount(0);
});

test('Taskboard_WhenMoveStale_ShouldSnapBackAndSayWhy', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, {
    ...hold,
    moveAnswer: {
      status: 409,
      body: { error: 'Changed by someone else.', code: 'conflict', current: {}, theirChanges: [] },
    },
  });
  await mount(slot, route);

  await drag(page, card(page, 'SHIP-173'), cell(page, 0, 1), 'cell');

  await expect.poll(() => server.moves.length).toBe(1);
  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'SHIP-173 was changed by someone else',
  );
  await expect(cell(page, 0, 0).getByTestId(ids.card)).toHaveCount(1);
  await expect(cell(page, 0, 1).getByTestId(ids.card)).toHaveCount(1);
  await expect.poll(() => server.reads).toBe(2);
});

// The two frames after a stubbed answer lands: whatever the page does with it has been drawn.
const settle = (page: Page) =>
  page.evaluate(
    () =>
      new Promise<void>((done) => requestAnimationFrame(() => requestAnimationFrame(() => done()))),
  );

test('Taskboard_WhenSameCardMovedTwice_ShouldNameTheRevisionTheFirstMoveWrote', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW, { ...hold, checkRevisions: true });
  await mount(slot, route);

  await card(page, 'SHIP-173').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect(card(page, 'SHIP-173')).toBeFocused();
  await page.keyboard.press('Alt+ArrowRight');

  await expect.poll(() => server.moves.length).toBe(2);
  expect(server.moves.map((m) => [m.stateId, m.expectedRevision])).toEqual([
    ['t-active', 1],
    ['t-closed', 2],
  ]);
  await expect(cell(page, 0, 2).getByTestId(ids.cardKey)).toHaveText(['SHIP-171', 'SHIP-173']);
  await expect(
    page.getByTestId(componentTestIds.toast).filter({ hasText: 'went back' }),
  ).toHaveCount(0);
});

test('Taskboard_WhenMoveSettlesWhileNextIsQueued_ShouldRefetchOnlyAfterTheLast', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW, { checkRevisions: true, gateMove: 1 });
  await mount(slot, route);

  await card(page, 'SHIP-173').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect(card(page, 'SHIP-173')).toBeFocused();
  await page.keyboard.press('Alt+ArrowRight');
  server.releaseMove();
  await expect.poll(() => server.moves.length).toBe(2);
  await expect.poll(() => server.served).toBe(2);
  await settle(page);
  expect(server.reads).toBe(2);
});

test('Taskboard_WhenRefetchIsInFlightAsAMoveStarts_ShouldKeepTheMoveOnScreen', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW, {
    checkRevisions: true,
    gateRead: 2,
    gateMove: 2,
  });
  await mount(slot, route);

  await card(page, 'SHIP-173').focus();
  await page.keyboard.press('Alt+ArrowRight');
  // The first move has settled and its refetch is on its way (held).
  await expect.poll(() => server.reads).toBe(2);
  await card(page, 'SHIP-172').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect(cell(page, 0, 2).getByTestId(ids.cardKey)).toHaveText(['SHIP-171', 'SHIP-172']);

  // The held refetch answers (with the board as it was) while the second move is still on its way.
  server.release();
  await expect.poll(() => server.served).toBe(2);
  await settle(page);
  await expect(cell(page, 0, 2).getByTestId(ids.cardKey)).toHaveText(['SHIP-171', 'SHIP-172']);
  server.releaseMove();
});

test('Taskboard_WhenMoveAnswerFlagsTheLastTask_ShouldOfferMoveToResolvedWithoutTheRefetch', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, withResolved(), {
    ...hold,
    moveAnswer: {
      status: 200,
      body: {
        written: { id: 'id-SHIP-173', key: 'SHIP-173', revision: 2, changes: [], notices: [] },
        parentId: 'id-SHIP-141',
        offerResolve: true,
      },
    },
  });
  await mount(slot, route);
  await expect(page.getByTestId(ids.rowOffer)).toHaveCount(0);

  await drag(page, card(page, 'SHIP-173'), cell(page, 0, 2), 'cell');

  await expect.poll(() => server.moves.length).toBe(1);
  await expect(page.getByTestId(ids.rowOffer)).toHaveCount(1);
});

test('Taskboard_WhenOfferDismissed_ShouldHideItAndStayOnTheStoryState', async ({ mount, page }) => {
  const server = await stubTaskboard(page, withOffer());
  await mount(slot, route);

  await page.getByTestId(ids.rowOfferDismiss).click();

  await expect(page.getByTestId(ids.rowOffer)).toHaveCount(0);
  expect(server.patches).toHaveLength(0);
});

test('Taskboard_WhenMoveToResolvedRefused_ShouldSayWhy', async ({ mount, page }) => {
  await stubTaskboard(page, withOffer(), {
    patchAnswer: {
      status: 409,
      body: { error: 'Changed by someone else.', code: 'conflict', current: {}, theirChanges: [] },
    },
  });
  await mount(slot, route);

  await page.getByTestId(ids.rowOfferResolve).click();

  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'SHIP-141 was changed by someone else',
  );
});

test('Taskboard_WhenPickedUpWithTwoInProgressStates_ShouldStartTheFirst', async ({
  mount,
  page,
}) => {
  const review = { id: 't-review', name: 'In review', category: 'inProgress' } as const;
  const view: TaskboardViewDto = {
    ...VIEW,
    columns: [VIEW.columns[0], VIEW.columns[1], { state: review, count: 0 }, VIEW.columns[2]],
  };
  const server = await stubTaskboard(page, view, hold);
  await mount(slot, route);

  await card(page, 'SHIP-173').hover();
  await card(page, 'SHIP-173').getByTestId(ids.cardPickUp).click();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0].stateId).toBe('t-active');
});

test('Taskboard_WhenPickedUp_ShouldShowMeAsAssigneeAtOnce', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, hold);
  await mount(slot, route);

  await card(page, 'SHIP-173').hover();
  await card(page, 'SHIP-173').getByTestId(ids.cardPickUp).click();

  await expect.poll(() => server.moves.length).toBe(1);
  await expect(
    card(page, 'SHIP-173').getByTestId(ids.cardAssignee).getByTestId(c.avatar),
  ).toHaveAttribute('title', 'Priya Shah');
});

test('Taskboard_WhenPickedUpWithoutMyPeopleRow_ShouldMakeIt', async ({ mount, page }) => {
  const view: TaskboardViewDto = {
    ...VIEW,
    peopleRows: VIEW.peopleRows.filter((row) => row.person?.id !== PRIYA.id),
  };
  const server = await stubTaskboard(page, view, { ...hold, saved: { groupBy: 'people' } });
  await mount(slot, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(2);

  await card(page, 'SHIP-173').hover();
  await card(page, 'SHIP-173').getByTestId(ids.cardPickUp).click();

  await expect.poll(() => server.moves.length).toBe(1);
  const mine = page
    .getByTestId(ids.row)
    .filter({ has: page.getByTestId(ids.rowTitle).filter({ hasText: 'Priya Shah' }) });
  await expect(mine.getByTestId(ids.card)).toHaveCount(1);
});

test('Taskboard_WhenOneOfTwoMovesFails_ShouldPutBackOnlyThatCard', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, {
    ...hold,
    gateMove: 1,
    refuseTask: 'id-SHIP-173',
  });
  await mount(slot, route);

  await card(page, 'SHIP-173').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect.poll(() => server.moves.length).toBe(1);
  await card(page, 'SHIP-172').focus();
  await page.keyboard.press('Alt+ArrowRight');
  await expect(cell(page, 0, 2).getByTestId(ids.cardKey)).toHaveText(['SHIP-171', 'SHIP-172']);

  server.releaseMove();

  await expect(
    page.getByTestId(componentTestIds.toast).filter({ hasText: 'SHIP-173 was changed' }),
  ).toHaveCount(1);
  await expect(cell(page, 0, 0).getByTestId(ids.cardKey)).toHaveText(['SHIP-173']);
  await expect.poll(() => server.moves.length).toBe(2);
  await expect(cell(page, 0, 2).getByTestId(ids.cardKey)).toHaveText(['SHIP-171', 'SHIP-172']);
});
