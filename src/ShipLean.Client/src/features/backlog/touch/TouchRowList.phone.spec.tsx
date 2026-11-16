import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { backlogView, itemRow, PROJECT } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { REMOVE_FIRST_ROW_ID, TouchListDragHarness } from '../ct/TouchListDragHarness';
import { ListMode } from '../list/ListMode';
import { backlogTestIds as ids } from '../testIds';

const ROWS = [1, 2, 3, 4, 5].map((n) => itemRow({ key: `SHIP-${n}`, rank: n, title: `Row ${n}` }));

// The Stories List on a phone while a row is held by its handle (SHIP-716 D2). Runs in the CT phone project.

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

// Lifts row 1 by its handle and holds the finger over row `over` (0-based): no release.
async function holdOver(page: Page, over: number) {
  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove', x: number, y: number) =>
    cdp.send('Input.dispatchTouchEvent', { type, touchPoints: [{ x, y }] });
  const rows = page.getByTestId(ids.row);
  const handle = (await rows.first().getByTestId(ids.touchHandle).boundingBox())!;
  const target = (await rows.nth(over).boundingBox())!;
  const x = Math.round(handle.x + handle.width / 2);
  const y = Math.round(handle.y + handle.height / 2);
  await send('touchStart', x, y);
  await page.clock.runFor(500);
  const endY = Math.round(target.y + target.height / 2);
  for (let at = y + 20; at < endY + 20; at += 20) {
    await send('touchMove', x + 50, Math.min(at, endY));
    await page.clock.runFor(60);
  }
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
  return target;
}

async function mountList(mount: Parameters<Parameters<typeof test>[2]>[0]['mount'], page: Page) {
  await page.clock.install({ time: 0 });
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  await expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length);
  await expect(page.getByTestId(ids.touchDropLine)).toHaveCount(0);
}

test('ListDrag_WhileOverATarget_ShouldShowTheInsertionLine', async ({ mount, page }) => {
  await mountList(mount, page);
  const target = await holdOver(page, 3);

  // No release: the line is above the row under the finger, and only one shows.
  const line = page.getByTestId(ids.touchDropLine);
  await expect(line).toHaveCount(1);
  const lineBox = (await line.boundingBox())!;
  expect(Math.abs(lineBox.y - target.y)).toBeLessThan(4);
});

test('ListDrag_WhileOverATarget_ShouldNotMoveTheRows', async ({ mount, page }) => {
  await mountList(mount, page);
  const rows = page.getByTestId(ids.row);
  const before = (await rows.nth(4).boundingBox())!.y;
  await holdOver(page, 3);
  await expect(page.getByTestId(ids.touchDropLine)).toHaveCount(1);
  // The line is drawn over the gap, so the row below the target keeps its place.
  expect((await rows.nth(4).boundingBox())!.y).toBe(before);
});

test('ListDrag_OverTheRowJustBelowTheLiftedOne_ShouldShowNoLine', async ({ mount, page }) => {
  await mountList(mount, page);
  await holdOver(page, 1);
  // Dropping there leaves the row where it is, so no line promises a move.
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
  await expect(page.getByTestId(ids.touchDropLine)).toHaveCount(0);
});

test('ListDrag_WhenTheLiftedRowLeavesTheList_ShouldDropTheLine', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  await mount(<TouchListDragHarness rows={ROWS} />);
  await expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length);
  await holdOver(page, 3);
  await expect(page.getByTestId(ids.touchDropLine)).toHaveCount(1);

  // A teammate's change removes the lifted row mid-drag: its handle never reports the end of the lift.
  await page.getByTestId(REMOVE_FIRST_ROW_ID).dispatchEvent('click');
  await expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length - 1);
  await expect(page.getByTestId(ids.touchDropLine)).toHaveCount(0);
});
