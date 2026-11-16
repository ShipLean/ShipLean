import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { backlogView, itemRow, PROJECT } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { backlogTestIds as ids } from '../testIds';

const ROWS = [1, 2, 3, 4, 5].map((n) => itemRow({ key: `SHIP-${n}`, rank: n, title: `Row ${n}` }));

// The Stories List on a phone while a row is held by its handle (SHIP-716 D2). Runs in the CT phone project.

test('ListDrag_WhileOverATarget_ShouldShowTheInsertionLine', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  await expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length);
  await expect(page.getByTestId(ids.touchDropLine)).toHaveCount(0);

  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove', x: number, y: number) =>
    cdp.send('Input.dispatchTouchEvent', { type, touchPoints: [{ x, y }] });
  const rows = page.getByTestId(ids.row);
  const handle = (await rows.first().getByTestId(ids.touchHandle).boundingBox())!;
  const target = (await rows.nth(3).boundingBox())!;
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

  // No release: the line is above the row under the finger, and only one shows.
  const line = page.getByTestId(ids.touchDropLine);
  await expect(line).toHaveCount(1);
  const lineBox = (await line.boundingBox())!;
  expect(Math.abs(lineBox.y - target.y)).toBeLessThan(4);
});
