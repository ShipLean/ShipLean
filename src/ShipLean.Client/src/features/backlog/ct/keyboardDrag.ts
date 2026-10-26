import { expect } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';

/**
 * Presses `key` until dnd-kit has answered it. The keyboard sensor listens for keys one macrotask after the grip turns
 * `aria-pressed`, so a key pressed straight after can be lost under load; `moved` is a locator assertion that holds
 * once the key has had its visible effect (a neighbour sliding). A key that was taken is not pressed again.
 */
export async function pressUntil(page: Page, key: string, moved: () => Promise<void>) {
  await expect(async () => {
    const taken = await moved().then(
      () => true,
      () => false,
    );
    if (!taken) await page.keyboard.press(key);
    await moved();
  }).toPass({ timeout: 10_000 });
}

/** Holds once `row` has slid up out of the way (dnd-kit moves it by a negative translate). */
export const slidUp = (row: Locator) => () =>
  expect(row).toHaveAttribute('style', /translate3d\(0px, -[1-9]/, { timeout: 500 });

/** Holds once `row` sits where it started. */
export const settled = (row: Locator) => () =>
  expect(row).not.toHaveAttribute('style', /translate3d\(0px, -?[1-9]/, { timeout: 500 });
