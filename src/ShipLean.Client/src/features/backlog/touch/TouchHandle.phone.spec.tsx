import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { TouchHandleHarness } from '../ct/TouchHandleHarness';
import { touchHandleHarnessIds as harness } from '../ct/touchHandleHarnessIds';
import { backlogTestIds as ids } from '../testIds';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

// TouchHandle (SHIP-716 D2) in its own harness, through real touch input: CDP Input.dispatchTouchEvent, so the browser's
// own touch pipeline (scrolling, cancelable touchmove) runs, as in K6's useLongPressDrag.phone.spec.tsx. Time is
// Playwright's clock, paused, and moved by hand. Runs in the CT phone project.

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

const rowOf = (page: Page, row: string) =>
  page.getByTestId(harness.row).and(page.locator(`[data-row-id="${row}"]`));
const handleOf = (page: Page, row: string) => rowOf(page, row).getByTestId(ids.touchHandle);
const log = (page: Page) => page.getByTestId(harness.logLine);

async function centre(locator: ReturnType<typeof rowOf>): Promise<Point> {
  const box = await locator.boundingBox();
  if (!box) throw new Error('no box');
  return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
}

async function arrange(page: Page) {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
}

test('TouchHandle_WhenHeldAndDraggedOntoARow_ShouldReportThatRow', async ({ mount, page }) => {
  await arrange(page);
  await mount(<TouchHandleHarness />);
  const touch = await touchInput(page);
  const from = await centre(handleOf(page, 'r3'));
  const to = await centre(rowOf(page, 'r6'));

  await touch.start(from);
  await page.clock.runFor(450);
  await touch.move(from, to);
  await expect(page.getByTestId(touchTestIds.dragGhost)).toContainText('Row r3');
  await touch.end();

  await expect(log(page)).toHaveText(['drop r3 on r6']);
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
});

test('TouchHandle_WhenRowSwipedOutsideTheHandle_ShouldScrollNotDrag', async ({ mount, page }) => {
  await arrange(page);
  await mount(<TouchHandleHarness />);
  const touch = await touchInput(page);
  const body = await rowOf(page, 'r8').boundingBox();
  if (!body) throw new Error('row r8 has no box');
  const from = { x: Math.round(body.x + body.width - 40), y: Math.round(body.y + body.height / 2) };

  await touch.start(from);
  await touch.move(from, { x: from.x, y: from.y - 300 });
  await touch.end();
  await page.clock.runFor(1000);

  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(100);
  await expect(page.getByTestId(touchTestIds.dragGhost)).toHaveCount(0);
  await expect(log(page)).toHaveCount(0);
});

test('TouchHandle_WhenHeldNearTheBottom_ShouldScrollThePageWhileLifted', async ({
  mount,
  page,
}) => {
  await arrange(page);
  await mount(<TouchHandleHarness />);
  const touch = await touchInput(page);
  const from = await centre(handleOf(page, 'r3'));
  const bottom = { x: from.x, y: 830 };

  await touch.start(from);
  await page.clock.runFor(450);
  await touch.move(from, bottom);
  await page.clock.runFor(1000);

  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(50);
  await touch.end();
  await expect(log(page)).toHaveCount(1);
});

test('TouchHandle_WhenHeldNearTheTop_ShouldScrollThePageBackUp', async ({ mount, page }) => {
  await arrange(page);
  await mount(<TouchHandleHarness />);
  await page.evaluate(() => window.scrollTo(0, 10_000));
  const start = await page.evaluate(() => window.scrollY);
  expect(start).toBeGreaterThan(150);
  const touch = await touchInput(page);
  const from = await centre(handleOf(page, 'r20'));
  const top = { x: from.x, y: 70 };

  await touch.start(from);
  await page.clock.runFor(450);
  await touch.move(from, top);
  await page.clock.runFor(1000);

  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeLessThan(start - 50);
  await touch.end();
});

test('TouchHandle_WhenDisabled_ShouldNotLift', async ({ mount, page }) => {
  await arrange(page);
  await mount(<TouchHandleHarness disabled />);
  const touch = await touchInput(page);
  const handle = handleOf(page, 'r3');
  await expect(handle).toBeDisabled();
  await expect(handle).toHaveCSS('touch-action', 'auto');
  const from = await centre(handle);

  await touch.start(from);
  await page.clock.runFor(600);
  await touch.move(from, await centre(rowOf(page, 'r6')));
  await touch.end();

  await expect(page.getByTestId(touchTestIds.dragGhost)).toHaveCount(0);
  await expect(log(page)).toHaveCount(0);
});

test('TouchHandle_ShouldBeAtLeast32pxWideAndFullRowHeight', async ({ mount, page }) => {
  await mount(<TouchHandleHarness />);

  for (const id of ['r1', 'r9']) {
    const handle = await handleOf(page, id).boundingBox();
    const row = await rowOf(page, id).boundingBox();
    if (!handle || !row) throw new Error('no box');
    expect(handle.width).toBeGreaterThanOrEqual(32);
    expect(Math.abs(handle.height - row.height)).toBeLessThanOrEqual(1);
  }
  const handle = handleOf(page, 'r1');
  await expect(handle.getByTestId(ids.rank)).toHaveText('1');
  const rank = await handle.getByTestId(ids.rank).boundingBox();
  const grip = await handle.locator('span[aria-hidden]').boundingBox();
  expect(rank!.y).toBeLessThan(grip!.y);
});

for (const theme of ['light', 'dark'] as const) {
  test(`TouchHandle_WhenLifted_ShouldMatchItsScreenshot_backlogs-drag_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await arrange(page);
    await mount(<TouchHandleHarness />);
    const touch = await touchInput(page);
    const from = await centre(handleOf(page, 'r3'));
    const to = { x: 210, y: await centre(rowOf(page, 'r6')).then((p) => p.y) };

    await touch.start(from);
    await page.clock.runFor(450);
    await touch.move(from, to);
    await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();

    await expect(page).toHaveScreenshot(`backlogs-drag.${theme}.png`, { animations: 'disabled' });
    await touch.end();
  });
}
