import { expect, test } from '@playwright/experimental-ct-react';
import { Dialog } from './Dialog';
import { SUBTITLE, LONG_TITLE } from './ct/subtitleDialogText';
import { SubtitleDialogHarness } from './ct/SubtitleDialogHarness';
import { DialogEscHarness } from './ct/DialogEscHarness';
import { DialogHarness } from './ct/DialogHarness';
import { harnessTestIds as harness } from './ct/harnessIds';
import { touchTestIds } from '../touch/testIds';
import { componentTestIds as ids } from './testIds';

test('Dialog_WhenEscPressed_ShouldClose', async ({ mount, page }) => {
  await mount(<DialogHarness />);
  await page.getByTestId(harness.ctDialogOpener).click();
  await expect(page.getByTestId(ids.dialog)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
});

test('Dialog_WhenCloseButtonClicked_ShouldClose', async ({ mount, page }) => {
  await mount(<DialogHarness />);
  await page.getByTestId(harness.ctDialogOpener).click();
  await page.getByTestId(ids.dialogClose).click();
  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
});

test('Dialog_WhenTabbed_ShouldKeepFocusInside', async ({ mount, page }) => {
  await mount(<DialogHarness />);
  await page.getByTestId(harness.ctDialogOpener).click();
  const dialog = page.getByTestId(ids.dialog);
  await expect(dialog).toBeFocused();
  for (let press = 0; press < 6; press += 1) {
    await page.keyboard.press('Tab');
    await expect(dialog.locator(':focus')).toHaveCount(1);
  }
  await page.keyboard.press('Shift+Tab');
  await expect(dialog.locator(':focus')).toHaveCount(1);
  await expect(page.getByTestId(harness.ctDialogOutside)).not.toBeFocused();
});

test('Dialog_WhenClosed_ShouldReturnFocus', async ({ mount, page }) => {
  await mount(<DialogHarness />);
  await page.getByTestId(harness.ctDialogOpener).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId(ids.dialog)).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(harness.ctDialogOpener)).toBeFocused();
});

test('Dialog_WhenBackdropClickedThenTabbed_ShouldKeepFocusInside', async ({ mount, page }) => {
  await mount(<DialogHarness />);
  await page.getByTestId(harness.ctDialogOpener).click();
  const dialog = page.getByTestId(ids.dialog);
  await expect(dialog).toBeFocused();
  await page.mouse.click(2, 2);
  await page.keyboard.press('Tab');
  await expect(dialog.locator(':focus')).toHaveCount(1);
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
});

test('Dialog_WhenWidthGiven_ShouldUseIt', async ({ mount, page }) => {
  await mount(
    <Dialog title="Wide" width="900px" onClose={() => undefined}>
      <span>body</span>
    </Dialog>,
  );
  await expect(page.getByTestId(ids.dialog)).toHaveCSS('width', '900px');
});

test('Dialog_WhenAFieldHandledEscape_ShouldNotClose', async ({ mount, page }) => {
  await mount(<DialogEscHarness />);
  const state = page.getByTestId(harness.ctDialogState);

  await page.getByTestId(harness.ctDialogEscField).press('Escape');
  await expect(page.getByTestId(ids.dialog)).toBeVisible();
  await expect(state).toHaveText('open');

  await page.getByTestId(ids.dialog).press('Escape');
  await expect(state).toHaveText('closed');
});

test('Dialog_WhenAbove900_ShouldNotRenderThePrimaryAction', async ({ mount, page }) => {
  await mount(
    <Dialog
      title="New feature"
      onClose={() => undefined}
      primaryAction={<button type="button">Create</button>}
    >
      <span>body</span>
    </Dialog>,
  );
  const dialog = page.getByTestId(ids.dialog);
  await expect(dialog.getByTestId(ids.dialogClose)).toBeVisible();
  await expect(dialog.getByTestId(touchTestIds.dialogPrimary)).toHaveCount(0);
  await expect(dialog.getByTestId(touchTestIds.dialogClose)).toHaveCount(0);
  await expect(dialog.getByRole('button', { name: 'Create' })).toHaveCount(0);
});

test('Dialog_WhenGivenASubtitle_ShouldNameItByTheTitleAndDescribeItBySubtitle', async ({
  mount,
  page,
}) => {
  await mount(<SubtitleDialogHarness />);
  const dialog = page.getByTestId(ids.dialog);
  await expect(dialog.getByTestId(ids.dialogSubtitle)).toHaveText(SUBTITLE);
  await expect(dialog.getByTestId(ids.dialogTitle)).toHaveText(LONG_TITLE);
  await expect(dialog).toHaveAccessibleName(LONG_TITLE);
  await expect(dialog).toHaveAccessibleDescription(SUBTITLE);
});

test('Dialog_WhenGivenASubtitle_ShouldKeepTheTitleOnOneLine', async ({ mount, page }) => {
  await mount(<SubtitleDialogHarness />);
  const title = (await page.getByTestId(ids.dialogTitle).boundingBox())!;
  const subtitle = (await page.getByTestId(ids.dialogSubtitle).boundingBox())!;
  expect(title.height).toBeLessThan(28);
  expect(subtitle.y).toBeGreaterThanOrEqual(title.y + title.height);
});

for (const theme of ['light', 'dark'] as const) {
  test(`Dialog_WhenGivenASubtitle_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<SubtitleDialogHarness />);
    await expect(page.getByTestId(ids.dialogSubtitle)).toBeVisible();
    await expect(page).toHaveScreenshot(`dialog-subtitle.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

test('Dialog_WhenGivenNoSubtitle_ShouldNotDescribeItself', async ({ mount, page }) => {
  await mount(
    <Dialog title="New feature" onClose={() => undefined}>
      <span>body</span>
    </Dialog>,
  );
  const dialog = page.getByTestId(ids.dialog);
  await expect(dialog).toHaveAccessibleName('New feature');
  await expect(dialog).not.toHaveAttribute('aria-describedby', /.*/);
  await expect(dialog.getByTestId(ids.dialogSubtitle)).toHaveCount(0);
});
