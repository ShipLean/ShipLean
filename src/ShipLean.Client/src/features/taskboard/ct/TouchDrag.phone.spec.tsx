import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { TaskboardMoveRequest } from '../taskboardApi';
import { touchTestIds } from '../../../touch/testIds';
import { taskboardTestIds as ids } from '../testIds';
import { AddressProbe } from './AddressProbe';
import { PhoneGridHarness } from './PhoneGridHarness';

// A task card lifted by a long press on the phone grid (SHIP-722): the drag half of D2. Real touch input through CDP and a
// paused Playwright clock, as touch/useLongPressDrag.phone.spec.tsx does. Runs in the CT phone project.

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100/board' } };
const MOVES = '**/api/work-items/f1/feature-board/moves';

interface Point {
  x: number;
  y: number;
}

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
// The task area of a story's lane on the first page (New).
const laneTasks = (page: Page, row: string) =>
  page
    .locator(`[data-testid="${touchTestIds.pagerPage}"][data-page-id="t-new"]`)
    .locator(`[data-testid="${ids.laneTasks}"][data-row-id="${row}"]`);

async function centreOf(target: ReturnType<Page['locator']>): Promise<Point> {
  const box = await target.boundingBox();
  if (!box) throw new Error('no box');
  return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
}

async function arrange(page: Page) {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
}

test('TaskCard_WhenHeldAndReleasedWithoutMoving_ShouldOpenItsActions', async ({ mount, page }) => {
  await arrange(page);
  await mount(
    <>
      <PhoneGridHarness board="feature" />
      <AddressProbe />
    </>,
    route,
  );
  const touch = await touchInput(page);

  await touch.start(await centreOf(card(page, 'SHIP-173').getByTestId(ids.cardKey)));
  await page.clock.runFor(450);
  await touch.end();

  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
  await expect(page.getByTestId(ids.address)).not.toHaveText(/\?item=/);
});

test('TaskCard_WhenDraggedOverALane_ShouldMarkItDropActive', async ({ mount, page }) => {
  await arrange(page);
  const sent = await captureMoves(page);
  await mount(<PhoneGridHarness board="parent" />, route);
  const touch = await touchInput(page);
  const from = await centreOf(card(page, 'SHIP-173').getByTestId(ids.cardKey));
  const lane = laneTasks(page, 'id-SHIP-150');
  const over = await centreOf(lane);

  await touch.start(from);
  await page.clock.runFor(450);
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
  await touch.move(from, over);
  await expect(lane).toHaveAttribute('data-drop-active', '');
  await touch.move(over, from);
  await expect(lane).not.toHaveAttribute('data-drop-active', '');
  await touch.move(from, over);
  await touch.end();

  await expect(lane).not.toHaveAttribute('data-drop-active', '');
  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0]).toMatchObject({
    taskId: 'id-SHIP-173',
    row: { grouping: 'stories', target: 'id-SHIP-150' },
    method: 'touch-drag',
  });
});
