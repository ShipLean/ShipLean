import { expect, test } from '@playwright/experimental-ct-react';
import { ToastHarness } from './ct/ToastHarness';
import { harnessTestIds as harness } from './ct/harnessIds';
import { componentTestIds as ids } from './testIds';

// The toast at touch widths (K4, SPRINT-7) on the phone project (390×844): the screen's width less 12px gutters, 12px
// above the bottom (the safe area is 0 here), the message on the left and ✕ at the right end.

const THEMES = ['light', 'dark'] as const;

test('Toast_AtTouchWidth_ShouldSpanTheWidthAboveTheSafeArea', async ({ mount, page }) => {
  await mount(<ToastHarness message="Priya moved SHIP-141 while you were editing" />);
  await page.getByTestId(harness.ctShowToast).tap();
  const toast = page.getByTestId(ids.toast);
  await expect(toast).toBeVisible();

  const box = (await toast.boundingBox())!;
  expect(box.x).toBe(12);
  expect(box.width).toBe(366);
  expect(box.y + box.height).toBe(844 - 12);
  const dismiss = (await toast.getByTestId(ids.toastDismiss).boundingBox())!;
  expect(dismiss.x + dismiss.width).toBeGreaterThan(box.x + box.width - 24);
});

test('Toast_AtTouchWidth_ShouldGiveTheDismissCrossATouchTargetSizedHitArea', async ({
  mount,
  page,
}) => {
  await mount(<ToastHarness message="Priya moved SHIP-141 while you were editing" />);
  await page.getByTestId(harness.ctShowToast).tap();
  const dismiss = page.getByTestId(ids.toast).getByTestId(ids.toastDismiss);
  await expect(dismiss).toBeVisible();

  const box = (await dismiss.boundingBox())!;
  expect(box.width).toBeGreaterThanOrEqual(32);
  expect(box.height).toBeGreaterThanOrEqual(32);
  // The hit area sits in the toast's padding: a one-line toast stays the 34px it was before the target grew.
  const toast = (await page.getByTestId(ids.toast).boundingBox())!;
  expect(toast.height).toBeLessThanOrEqual(36);
});

for (const theme of THEMES) {
  test(`Toast_AtTouchWidth_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<ToastHarness message="Priya moved SHIP-141 while you were editing" />);
    await page.getByTestId(harness.ctShowToast).tap();
    const toast = page.getByTestId(ids.toast);
    await expect(toast).toBeVisible();

    await expect(toast).toHaveScreenshot(`toast.${theme}.png`, { animations: 'disabled' });
  });
}
