import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../../touch/testIds';
import { BoardHarness } from '../../ct/BoardHarness';
import { storiesBoard } from '../../ct/fixtures';
import { stubBoard } from '../../ct/stubApi';
import { boardTestIds as ids } from '../../testIds';

// The ‹ › edge hints while a card is lifted on a phone board (SHIP-1626 BACKLOG-21): holding at either edge turns the
// column page, so both edges say so while the card is up, and neither once it is down. Runs in the CT phone project.

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
const route = { hooksConfig: { route: '/p/SHIP/boards/stories' } };

async function liftFirstCard(page: Page) {
  const cdp = await page.context().newCDPSession(page);
  const box = (await page.getByTestId(ids.card).first().boundingBox())!;
  const from = { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
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
  // The clock goes in before the mount (as TouchDrag.phone.spec.tsx does); the 450ms lift is moved by hand.
  await page.clock.install({ time: 0 });
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await expect(page.getByTestId(ids.card).first()).toBeVisible();
  const drop = await liftFirstCard(page);

  await expect(hint(page, 'left')).toBeVisible();
  await expect(hint(page, 'right')).toBeVisible();
  const left = (await hint(page, 'left').boundingBox())!;
  const right = (await hint(page, 'right').boundingBox())!;
  expect(left.x).toBe(0);
  expect(right.x + right.width).toBe(390);
  await drop();
});

test('CardDrag_AfterDrop_ShouldHideThem', async ({ mount, page }) => {
  // The clock goes in before the mount (as TouchDrag.phone.spec.tsx does); the 450ms lift is moved by hand.
  await page.clock.install({ time: 0 });
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await expect(page.getByTestId(ids.card).first()).toBeVisible();
  const drop = await liftFirstCard(page);
  await expect(page.getByTestId(touchTestIds.dragEdgeHint)).toHaveCount(2);

  await drop();

  await expect(page.getByTestId(touchTestIds.dragGhost)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.dragEdgeHint)).toHaveCount(0);
});
