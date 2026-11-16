import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { touchTestIds } from '../../../touch/testIds';
import { PhoneGridHarness } from '../ct/PhoneGridHarness';
import { taskboardTestIds as ids } from '../testIds';

// The ‹ › edge hints while a task is lifted on the phone grid (SHIP-1626 TBOARD-12): holding at either edge turns the
// page, so both edges say so while the card is up, and neither once it is down. Real touch input through CDP and a paused
// clock, as TouchDrag.phone.spec.tsx does. Runs in the CT phone project. The sprint Taskboard and the Feature board share
// this grid and this hook; the harness drives it through the Feature board.

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100/board' } };
const MOVES = '**/api/work-items/f1/feature-board/moves';

interface Point {
  x: number;
  y: number;
}

async function liftCard(page: Page, key: string) {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  const cdp = await page.context().newCDPSession(page);
  const box = (await page
    .getByTestId(ids.card)
    .and(page.locator(`[data-key="${key}"]`))
    .first()
    .getByTestId(ids.cardKey)
    .boundingBox())!;
  const from: Point = {
    x: Math.round(box.x + box.width / 2),
    y: Math.round(box.y + box.height / 2),
  };
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [from] });
  await page.clock.runFor(450);
  await cdp.send('Input.dispatchTouchEvent', {
    type: 'touchMove',
    touchPoints: [{ x: from.x, y: from.y + 20 }],
  });
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
  return () => cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

const hint = (page: Page, edge: 'left' | 'right') =>
  page.getByTestId(touchTestIds.dragEdgeHint).and(page.locator(`[data-edge="${edge}"]`));

test('CardDrag_WhileLifted_ShouldShowBothEdgeHints', async ({ mount, page }) => {
  await mount(<PhoneGridHarness board="feature" />, route);
  const drop = await liftCard(page, 'SHIP-173');

  await expect(hint(page, 'left')).toBeVisible();
  await expect(hint(page, 'right')).toBeVisible();
  const left = (await hint(page, 'left').boundingBox())!;
  const right = (await hint(page, 'right').boundingBox())!;
  expect(left.x).toBe(0);
  expect(right.x + right.width).toBe(390);
  await drop();
});

test('CardDrag_AfterDrop_ShouldHideThem', async ({ mount, page }) => {
  await page.route(MOVES, (r) =>
    r.fulfill({ json: { written: { revision: 2 }, parentId: null, offerResolve: false } }),
  );
  await mount(<PhoneGridHarness board="feature" />, route);
  const drop = await liftCard(page, 'SHIP-173');
  await expect(page.getByTestId(touchTestIds.dragEdgeHint)).toHaveCount(2);

  await drop();

  await expect(page.getByTestId(touchTestIds.dragGhost)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.dragEdgeHint)).toHaveCount(0);
});
