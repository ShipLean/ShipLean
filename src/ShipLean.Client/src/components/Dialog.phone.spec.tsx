import { expect, test } from '@playwright/experimental-ct-react';
import { saveBarHarnessIds as harness } from '../touch/ct/saveBarHarnessIds';
import { touchTestIds as touch } from '../touch/testIds';
import { FullScreenDialogHarness } from './ct/FullScreenDialogHarness';
import { componentTestIds as ids } from './testIds';

// The full-screen Dialog at touch (K5, SHIP-805 C9) on the CT phone project (390×844).

test('Dialog_WhenOpenOnAPhone_ShouldFillTheScreen', async ({ mount, page }) => {
  await mount(<FullScreenDialogHarness />);
  const box = await page.getByTestId(ids.dialog).boundingBox();
  expect(box).toEqual({ x: 0, y: 0, width: 390, height: 844 });
});

test('Dialog_WhenOpenOnAPhone_ShouldPutCloseTopLeftAndPrimaryTopRight', async ({ mount, page }) => {
  await mount(<FullScreenDialogHarness />);
  const dialog = page.getByTestId(ids.dialog);
  const close = await dialog.getByTestId(touch.dialogClose).boundingBox();
  const primary = await dialog.getByTestId(touch.dialogPrimary).boundingBox();
  if (!close || !primary) throw new Error('the ✕ and the primary action should both render');

  expect(close.x).toBeLessThan(16);
  expect(close.y).toBeLessThan(16);
  expect(close.width).toBeGreaterThanOrEqual(32);
  expect(close.height).toBeGreaterThanOrEqual(32);
  expect(primary.x + primary.width).toBeGreaterThan(390 - 16);
  expect(primary.y).toBeLessThan(16);
  await expect(dialog.getByTestId(touch.dialogClose)).toHaveAttribute('aria-label', 'Close');
  await expect(dialog.getByTestId(touch.dialogPrimary)).toContainText('Create');
});

test('Dialog_WhenCrossTapped_ShouldClose', async ({ mount, page }) => {
  await mount(<FullScreenDialogHarness />);
  await page.getByTestId(touch.dialogClose).tap();
  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
  await expect(page.getByTestId(harness.ctDialogState)).toHaveText('closed');
});

test('Dialog_WhenOnAPhone_ShouldNotRenderTheDesktopCross', async ({ mount, page }) => {
  await mount(<FullScreenDialogHarness />);
  const dialog = page.getByTestId(ids.dialog);
  await expect(dialog.getByTestId(touch.dialogClose)).toBeVisible();
  await expect(dialog.getByTestId(ids.dialogClose)).toHaveCount(0);
  await expect(dialog.getByRole('button', { name: 'Close' })).toHaveCount(1);
});

test('Dialog_WhenBodyIsLongOnAPhone_ShouldScrollTheBodyAndKeepTheHeader', async ({
  mount,
  page,
}) => {
  await mount(<FullScreenDialogHarness rows={60} />);
  const dialog = page.getByTestId(ids.dialog);
  const end = page.getByTestId(harness.ctDialogBodyEnd);
  await expect(end).not.toBeInViewport();

  await end.scrollIntoViewIfNeeded();
  await expect(end).toBeInViewport();
  const close = await dialog.getByTestId(touch.dialogClose).boundingBox();
  expect(close?.y).toBeLessThan(16);
  expect(await dialog.evaluate((panel) => panel.scrollTop)).toBe(0);
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
  expect(await dialog.boundingBox()).toEqual({ x: 0, y: 0, width: 390, height: 844 });
});

test('Dialog_WhenEscPressedOnAPhone_ShouldClose', async ({ mount, page }) => {
  await mount(<FullScreenDialogHarness />);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
});

for (const theme of ['light', 'dark'] as const) {
  test(`Dialog_WhenOpenOnAPhone_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<FullScreenDialogHarness />);
    await expect(page.getByTestId(ids.dialog)).toBeVisible();
    await expect(page).toHaveScreenshot(`dialog-full-screen.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
