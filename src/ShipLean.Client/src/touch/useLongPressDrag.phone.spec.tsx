import { expect, test, type Page } from '@playwright/experimental-ct-react';
import { DragHarness } from './ct/DragHarness';
import { dragHarnessIds as ids } from './ct/dragHarnessIds';
import { touchTestIds } from './testIds';

// useLongPressDrag and TouchDragGhost (K6) through real touch input: CDP Input.dispatchTouchEvent, so the browser's own
// touch pipeline (scrolling, cancelable touchmove) runs. Time is Playwright's clock, paused, and moved by hand.

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
    /** Moves in 20px steps, as a finger would, from `from` to `to`. */
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

async function centreOf(page: Page, card: string): Promise<Point> {
  const box = await page.locator(`[data-card="${card}"]`).boundingBox();
  if (!box) throw new Error(`card ${card} has no box`);
  return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
}

const log = (page: Page) => page.getByTestId(ids.logLine);

async function arrange(page: Page) {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
}

test('LongPressDrag_WhenHeldAndMovedOntoATarget_ShouldDropThere', async ({ mount, page }) => {
  await arrange(page);
  await mount(<DragHarness />);
  const touch = await touchInput(page);
  const from = await centreOf(page, 'a2');
  const to = await centreOf(page, 'b2');

  await touch.start(from);
  await page.clock.runFor(450);
  await expect(log(page)).toHaveText(['lift a2']);
  await touch.move(from, to);
  await touch.end();

  await expect(log(page)).toHaveText(['lift a2', 'drop a2 on zone-b']);
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
});

test('LongPressDrag_WhenSwipedWithoutHolding_ShouldScrollThePage', async ({ mount, page }) => {
  await arrange(page);
  await mount(<DragHarness />);
  const touch = await touchInput(page);
  const from = await centreOf(page, 'b2');

  await touch.start(from);
  await touch.move(from, { x: from.x, y: from.y - 300 });
  await touch.end();
  await page.clock.runFor(1000);

  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(100);
  await expect(log(page)).toHaveCount(0);
});

test('LongPressDrag_WhenHeldAtTheEdge_ShouldCallOnEdge', async ({ mount, page }) => {
  await arrange(page);
  await mount(<DragHarness />);
  const touch = await touchInput(page);
  const from = await centreOf(page, 'a2');
  const edge = { x: 380, y: from.y };

  await touch.start(from);
  await page.clock.runFor(450);
  await touch.move(from, edge);
  await page.clock.runFor(450);
  await expect(log(page)).toHaveText(['lift a2']);
  await page.clock.runFor(100);
  await expect(log(page)).toHaveText(['lift a2', 'edge right']);
  await page.clock.runFor(800);
  await expect(log(page)).toHaveText(['lift a2', 'edge right', 'edge right']);
  await touch.end();
});

test('LongPressDrag_WhenReleasedWithoutMoving_ShouldCallOnTapWithoutMove', async ({ mount, page }) => {
  await arrange(page);
  await mount(<DragHarness />);
  const touch = await touchInput(page);

  await touch.start(await centreOf(page, 'a3'));
  await page.clock.runFor(450);
  await touch.end();

  await expect(log(page)).toHaveText(['lift a3', 'tap a3']);
  await expect(page.getByTestId(touchTestIds.dragGhost)).toHaveCount(0);
});

test('LongPressDrag_WhenHandleOnly_ShouldOnlyStartOnTheHandle', async ({ mount, page }) => {
  await arrange(page);
  await mount(<DragHarness handle />);
  const touch = await touchInput(page);
  const card = page.locator('[data-card="a2"]');
  const handle = card.getByTestId(ids.handle);
  await expect(handle).toHaveCSS('touch-action', 'none');
  await expect(card).toHaveCSS('touch-action', 'auto');

  const body = await card.boundingBox();
  if (!body) throw new Error('card a2 has no box');
  await touch.start({ x: Math.round(body.x + body.width - 20), y: Math.round(body.y + body.height / 2) });
  await page.clock.runFor(600);
  await touch.end();
  await expect(log(page)).toHaveCount(0);

  const grip = await handle.boundingBox();
  if (!grip) throw new Error('handle has no box');
  await touch.start({ x: Math.round(grip.x + grip.width / 2), y: Math.round(grip.y + grip.height / 2) });
  await page.clock.runFor(450);
  await expect(log(page)).toHaveText(['lift a2']);
  await touch.end();
});

test('LongPressDrag_WhenLifted_ShouldShowTheGhostUnderTheFinger', async ({ mount, page }) => {
  await arrange(page);
  await mount(<DragHarness />);
  const touch = await touchInput(page);
  const from = await centreOf(page, 'a2');
  const to = { x: 200, y: 520 };

  await touch.start(from);
  await page.clock.runFor(450);
  await touch.move(from, to);

  const ghost = page.getByTestId(touchTestIds.dragGhost);
  await expect(ghost).toContainText('Card a2');
  await expect(ghost).toHaveCSS('pointer-events', 'none');
  await expect(page.locator('[data-card="a2"]')).toHaveCSS('opacity', '0.4');
  await expect
    .poll(async () => {
      const box = await ghost.boundingBox();
      return box !== null && box.x < to.x && to.x < box.x + box.width && box.y < to.y && to.y < box.y + box.height;
    })
    .toBe(true);
  const under = await page.evaluate(({ x, y }) => document.elementFromPoint(x, y)?.closest('[data-drop-target]')?.getAttribute('data-drop-target'), to);
  expect(under).toBe('zone-b');
  await touch.end();
});

test('LongPressDrag_WhenLiftedNearTheTop_ShouldShowTheGhostAboveTheTopBar', async ({ mount, page }) => {
  await arrange(page);
  await mount(<DragHarness />);
  const touch = await touchInput(page);
  const from = await centreOf(page, 'a1');
  const top = { x: 195, y: 20 };

  await touch.start(from);
  await page.clock.runFor(450);
  await touch.move(from, top);

  const ghost = page.getByTestId(touchTestIds.dragGhost);
  const bar = page.getByTestId(ids.topBar);
  await expect(ghost).toHaveCSS('z-index', '80');
  await expect(bar).toHaveCSS('z-index', '30');
  await expect
    .poll(async () => {
      const box = await ghost.boundingBox();
      return box !== null && box.y < top.y && top.y < box.y + box.height;
    })
    .toBe(true);
  // Painted over the bar: with the ghost's pointer-events switched back on, the top hit at the finger is the ghost.
  const hit = await page.evaluate(({ x, y }) => {
    const ghostElement = document.querySelector<HTMLElement>('[data-testid="touch-drag-ghost"]');
    if (!ghostElement) return null;
    ghostElement.style.pointerEvents = 'auto';
    const found = document.elementFromPoint(x, y)?.closest('[data-testid]')?.getAttribute('data-testid');
    ghostElement.style.pointerEvents = '';
    return found;
  }, top);
  expect(hit).toBe(touchTestIds.dragGhost);
  await touch.end();
});

for (const theme of ['light', 'dark'] as const) {
  test(`LongPressDrag_WhenLifted_ShouldMatchItsScreenshot_drag-lifted_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await arrange(page);
    await mount(<DragHarness />);
    const touch = await touchInput(page);
    const from = await centreOf(page, 'a2');
    const to = { x: 210, y: 500 };

    await touch.start(from);
    await page.clock.runFor(450);
    await touch.move(from, to);
    await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();

    await expect(page).toHaveScreenshot(`drag-lifted.${theme}.png`, { animations: 'disabled' });
    await touch.end();
  });
}
