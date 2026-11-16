import { expect } from '@playwright/experimental-ct-react';
import { boardTestIds } from '../../../features/board/testIds';
import { touchTestIds } from '../../../touch/testIds';
import type { Page } from '../shoot';

interface Point {
  x: number;
  y: number;
}

const cardOf = (page: Page, key: string) =>
  page
    .getByTestId(boardTestIds.card)
    .filter({ has: page.getByTestId(boardTestIds.cardKey).filter({ hasText: key }) });

/** The centre of a card, or of a cell's end: the cell's box, a few pixels above its bottom edge. */
const centreOf = (box: { x: number; y: number; width: number; height: number }): Point => ({
  x: Math.round(box.x + box.width / 2),
  y: Math.round(box.y + box.height / 2),
});

/**
 * Long-presses the card `from` (CDP touch, the clock moved by hand) and moves it over the card `over`, or over the end of
 * the cell `overCell` (a locator) when that is given; the finger stays down. The caller installs the clock before mounting
 * and turns the pager so both cards are on screen.
 */
export async function holdCardOver(
  page: Page,
  from: string,
  over: string | { cell: ReturnType<Page['locator']> },
) {
  await expect(cardOf(page, from)).toBeInViewport({ ratio: 1 });
  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove', point: Point) =>
    cdp.send('Input.dispatchTouchEvent', { type, touchPoints: [point] });
  const start = centreOf((await cardOf(page, from).boundingBox())!);
  let end: Point;
  if (typeof over === 'string') {
    await expect(cardOf(page, over)).toBeInViewport({ ratio: 1 });
    end = centreOf((await cardOf(page, over).boundingBox())!);
  } else {
    await expect(over.cell).toBeInViewport();
    const box = (await over.cell.boundingBox())!;
    // Below the cell's last card, inside its padding: over the cell, not over a card.
    end = { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height - 6) };
  }
  await send('touchStart', start);
  await page.clock.runFor(450);
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
  const steps = Math.max(1, Math.ceil(Math.hypot(end.x - start.x, end.y - start.y) / 20));
  for (let i = 1; i <= steps; i++) {
    await send('touchMove', {
      x: Math.round(start.x + ((end.x - start.x) * i) / steps),
      y: Math.round(start.y + ((end.y - start.y) * i) / steps),
    });
  }
  await expect(page.getByTestId(boardTestIds.dropLine)).toHaveCount(1);
}
