import { expect } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';
import { backlogTestIds as ids } from '../testIds';

/**
 * Picks a row up by its grip and puts it down on `target`: above its middle, below it, or on it. It waits for dnd-kit
 * to name `target` as the drop target (`data-over`) before the button is released, because a release that races the
 * last render drops on the previous target.
 */
export async function dragRow(
  page: Page,
  row: Locator,
  target: Locator,
  where: 'above' | 'below' | 'on' = 'on',
) {
  const grip = (await row.getByTestId(ids.grip).boundingBox())!;
  const box = (await target.boundingBox())!;
  const offset = where === 'above' ? -12 : where === 'below' ? 12 : 0;
  const startX = grip.x + grip.width / 2;
  const startY = grip.y + grip.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + 5, startY + 10, { steps: 4 });
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2 + offset, { steps: 12 });
  await expect(target).toHaveAttribute('data-over', 'true');
  // A row shows on which edge the dragged one lands.
  if (where !== 'on')
    await expect(target).toHaveAttribute('data-edge', where === 'above' ? 'top' : 'bottom');
  await page.mouse.up();
}
