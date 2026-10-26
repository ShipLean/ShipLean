import { expect, test } from '@playwright/experimental-ct-react';
import { ToastHarness } from './ct/ToastHarness';
import { harnessTestIds as harness } from './ct/harnessIds';
import { componentTestIds as ids } from './testIds';

test('ToastHost_WhenShown_ShouldListTheMessage', async ({ mount, page }) => {
  await mount(<ToastHarness message="Moved SHIP-1" />);
  await page.getByTestId(harness.ctShowToast).click();
  await expect(page.getByTestId(ids.toastHost).getByTestId(ids.toast)).toContainText(
    'Moved SHIP-1',
  );
});

test('ToastHost_WhenActionClicked_ShouldCallIt', async ({ mount, page }) => {
  await mount(<ToastHarness message="Moved" actionLabel="Undo" />);
  await page.getByTestId(harness.ctShowToast).click();
  await page.getByTestId(ids.toastAction).click();
  await expect(page.getByTestId(harness.ctToastActionCount)).toHaveText('1');
});

test('ToastHost_WhenDurationPasses_ShouldDismissTheToast', async ({ mount, page }) => {
  await mount(<ToastHarness message="Gone soon" durationMs={300} />);
  await page.getByTestId(harness.ctShowToast).click();
  await expect(page.getByTestId(ids.toast)).toBeVisible();
  await expect(page.getByTestId(ids.toast)).toHaveCount(0);
});

test('ToastHost_WhenDismissClicked_ShouldRemoveTheToast', async ({ mount, page }) => {
  await mount(<ToastHarness message="Hello" />);
  await page.getByTestId(harness.ctShowToast).click();
  await page.getByTestId(ids.toastDismiss).click();
  await expect(page.getByTestId(ids.toast)).toHaveCount(0);
});

test('ToastHost_WhenActionClicked_ShouldDismissTheToast', async ({ mount, page }) => {
  await mount(<ToastHarness message="Moved" actionLabel="Undo" />);
  await page.getByTestId(harness.ctShowToast).click();
  await page.getByTestId(ids.toastAction).click();
  await expect(page.getByTestId(ids.toast)).toHaveCount(0);
});
