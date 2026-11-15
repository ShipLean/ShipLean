import { expect, test } from '@playwright/experimental-ct-react';
import { TouchRowHarness } from '../ct/TouchRowHarness';
import { itemRow, ROWS } from '../ct/fixtures';
import { backlogTestIds as ids } from '../testIds';

// The Stories touch row and list on a phone (SHIP-716 D1). Runs in the CT phone project.

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
interface Point {
  x: number;
  y: number;
}

// Real touch input (CDP), as TouchHandle.phone.spec.tsx does it.
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

async function centre(locator: ReturnType<Page['locator']>): Promise<Point> {
  const box = await locator.boundingBox();
  if (!box) throw new Error('no box');
  return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
}

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
// The stand-in handle TouchRowHarness renders through renderHandle.
const HARNESS_HANDLE_ID = 'harness-touch-handle';
// The drop log TouchRowHarness renders when given dropLog.
const HARNESS_DROP_LOG_ID = 'harness-touch-drop-log';

const LONG_PARENT = itemRow({
  key: 'SHIP-171',
  title: 'Saved cards at checkout',
  sprint: { id: 'sp24', name: 'Sprint 24', status: 'current' },
  parent: {
    id: 'f2',
    key: 'SHIP-95',
    type: { id: 'feature', name: 'Feature' },
    title: 'Checkout with saved payment methods for returning customers on every device',
  },
});

test('TouchRow_WhenNoEstimate_ShouldShowWarnQuestionMark', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} />, route);

  const unestimated = page.locator(`[data-testid="${ids.row}"][data-key="SHIP-160"]`);
  const points = unestimated.getByTestId(ids.points);
  await expect(points).toHaveText('?');
  await expect(points).toHaveClass(/text-caution/);
  await expect(
    page.locator(`[data-testid="${ids.row}"][data-key="SHIP-158"]`).getByTestId(ids.points),
  ).toHaveText('5');
});

test('TouchRow_WhenParentLong_ShouldWrapToOwnLine', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={[LONG_PARENT]} />, route);

  const row = page.getByTestId(ids.row);
  const key = await row.getByTestId(ids.key).boundingBox();
  const parent = row.getByTestId(ids.parent);
  const box = await parent.boundingBox();
  // Below the key, and its whole title shows (wrapped, never cut).
  expect(box!.y).toBeGreaterThan(key!.y + key!.height - 1);
  await expect(parent).toContainText(
    'Checkout with saved payment methods for returning customers on every device',
  );
  const cut = await parent.evaluate(
    (element) =>
      element.scrollWidth > element.clientWidth ||
      element.getBoundingClientRect().right > document.documentElement.clientWidth,
  );
  expect(cut).toBe(false);
});

test('TouchRowList_WhenSelecting_ShouldShowCheckboxesInTheHandleSlot', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} withHandle selecting />, route);

  const slots = page.getByTestId(ids.touchRowHandle);
  await expect(slots.getByTestId(ids.touchRowSelect)).toHaveCount(ROWS.length);
  await expect(page.getByTestId(HARNESS_HANDLE_ID)).toHaveCount(0);
  await page.getByTestId(ids.row).first().tap();
  await expect(page.getByTestId(ids.row).first()).toHaveAttribute('data-selected', 'true');
  await expect(slots.getByTestId(ids.touchRowSelect).first()).toBeChecked();
});

test('TouchRowList_WhenRenderHandleGiven_ShouldPutItInTheSlot', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} withHandle />, route);

  const slots = page.getByTestId(ids.touchRowHandle);
  await expect(slots.getByTestId(HARNESS_HANDLE_ID)).toHaveCount(ROWS.length);
  await expect(slots.getByTestId(ids.rank)).toHaveCount(0);
});

test('TouchRowList_WhenFiltering_ShouldNotRenderHandles', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} withHandle handles={false} />, route);

  const slots = page.getByTestId(ids.touchRowHandle);
  await expect(slots.getByTestId(ids.rank)).toHaveText(['3', '7']);
  await expect(page.getByTestId(HARNESS_HANDLE_ID)).toHaveCount(0);
});

test('TouchRow_ShouldCarryItsDropTarget', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} />, route);

  await expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length);
  for (const row of ROWS) {
    await expect(page.locator(`[data-drop-target="${row.id}"]`)).toHaveAttribute(
      'data-key',
      row.key,
    );
  }
});

test('TouchRowList_WhenHandlesAllowed_ShouldShowATouchHandlePerRow', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} dropLog />, route);

  const slots = page.getByTestId(ids.touchRowHandle);
  await expect(slots.getByTestId(ids.touchHandle)).toHaveCount(ROWS.length);
  await expect(slots.getByTestId(ids.rank)).toHaveText(['3', '7']);
});

test('TouchRowList_WhenSelecting_ShouldNotShowTouchHandles', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} dropLog selecting />, route);

  await expect(page.getByTestId(ids.touchRowSelect)).toHaveCount(ROWS.length);
  await expect(page.getByTestId(ids.touchHandle)).toHaveCount(0);
});

test('TouchRowList_WhenHandleDroppedOnARow_ShouldCallTheReorderWrite', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  await mount(<TouchRowHarness rows={ROWS} dropLog />, route);
  const touch = await touchInput(page);
  const handle = page.getByTestId(ids.touchHandle).first();
  const target = page.locator(`[data-drop-target="${ROWS[1].id}"]`);
  const from = await centre(handle);
  const to = await centre(target);

  await touch.start(from);
  await page.clock.runFor(450);
  await touch.move(from, to);
  await touch.end();

  await expect(page.getByTestId(HARNESS_DROP_LOG_ID).getByRole('listitem')).toHaveText([
    `${ROWS[0].id} on ${ROWS[1].id}`,
  ]);
});
