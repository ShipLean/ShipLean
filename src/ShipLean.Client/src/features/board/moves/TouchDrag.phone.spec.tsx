import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardViewDto } from '@shiplean/core/api/views';
import { touchTestIds } from '../../../touch/testIds';
import { BoardHarness } from '../ct/BoardHarness';
import { card, cell, lane, storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

// The long-press drag of a card on a phone (SHIP-719, L719d). Runs in the CT phone project. Touch is CDP
// Input.dispatchTouchEvent; time is Playwright's clock, moved by hand.

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
interface Point {
  x: number;
  y: number;
}
const route = { hooksConfig: { route: '/p/SHIP/boards/stories' } };

async function touchInput(page: Page) {
  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove' | 'touchEnd', point?: Point) =>
    cdp.send('Input.dispatchTouchEvent', { type, touchPoints: point ? [point] : [] });
  return {
    start: (point: Point) => send('touchStart', point),
    async move(from: Point, to: Point) {
      const steps = Math.max(1, Math.ceil(Math.hypot(to.x - from.x, to.y - from.y) / 20));
      for (let i = 1; i <= steps; i++) {
        await send('touchMove', {
          x: from.x + ((to.x - from.x) * i) / steps,
          y: from.y + ((to.y - from.y) * i) / steps,
        });
      }
    },
    end: () => send('touchEnd'),
  };
}

const cardKeyed = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });

async function centreOf(locator: ReturnType<typeof cardKeyed>): Promise<Point> {
  const box = (await locator.boundingBox())!;
  return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
}

// Two cards in the default lane's New cell.
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

test('TouchDrag_WhenSwipeStartsOnCard_ShouldScrollPager', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  const server = await stubBoard(page, boardWithTwoNew());
  await mount(<BoardHarness />, route);
  const touch = await touchInput(page);
  const track = page.getByTestId(touchTestIds.pager);
  const from = await centreOf(cardKeyed(page, 'SHIP-164'));

  await touch.start(from);
  await touch.move(from, { x: 20, y: from.y });
  await touch.end();
  await page.clock.runFor(1000);

  await expect.poll(() => track.evaluate((node) => node.scrollLeft)).toBeGreaterThan(50);
  expect(server.moves).toHaveLength(0);
});

test('TouchDrag_WhenDroppedOnCard_ShouldPlaceAbove', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  const server = await stubBoard(page, boardWithTwoNew());
  await mount(<BoardHarness />, route);
  const touch = await touchInput(page);
  const from = await centreOf(cardKeyed(page, 'SHIP-164'));
  const to = await centreOf(cardKeyed(page, 'SHIP-163'));

  await touch.start(from);
  await page.clock.runFor(450);
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
  await touch.move(from, to);
  await expect(page.getByTestId(ids.dropLine)).toHaveCount(1);
  await touch.end();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    itemId: 'id-SHIP-164',
    columnId: 'col-new',
    beforeItemId: 'id-SHIP-163',
    method: 'touch-drag',
  });
  await expect(page.getByTestId(touchTestIds.dragGhost)).toHaveCount(0);
});

test('MoveTo_WhenPickedFromSheet_ShouldSendSheetMethod', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);

  await cardKeyed(page, 'SHIP-160').getByTestId(touchTestIds.moreButton).tap();
  await page
    .getByTestId(touchTestIds.sheet)
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="move-to"]`)
    .tap();
  await page
    .getByTestId(touchTestIds.sheet)
    .locator(
      `[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="column:col-prog:false"]`,
    )
    .tap();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]?.method).toBe('sheet');
});
