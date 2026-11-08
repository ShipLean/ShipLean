import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../components/testIds';
import { SaveBarHarness } from './ct/SaveBarHarness';
import { saveBarHarnessIds as harness } from './ct/saveBarHarnessIds';
import { touchTestIds as ids } from './testIds';

// The touch Save bar (K5, SHIP-805 rule 15) on the CT phone project (390×844).

test('SaveBar_WhenDirty_ShouldShowMessageDiscardAndSaveAtTheBottom', async ({ mount, page }) => {
  await mount(<SaveBarHarness />);
  const bar = page.getByTestId(ids.saveBar);
  const box = await bar.boundingBox();
  expect(box?.x).toBe(0);
  expect(box?.width).toBe(390);
  expect((box?.y ?? 0) + (box?.height ?? 0)).toBe(844);
  await expect(bar).toHaveCSS('z-index', '40');

  const message = await bar.getByTestId(ids.saveBarMessage).boundingBox();
  const discard = bar.getByTestId(ids.saveBarDiscard);
  const save = bar.getByTestId(ids.saveBarSave);
  await expect(discard).toHaveText('Discard');
  await expect(save).toHaveText('Save');
  const discardBox = await discard.boundingBox();
  const saveBox = await save.boundingBox();
  if (!message || !discardBox || !saveBox)
    throw new Error('message, Discard and Save should render');
  expect(message.x).toBeLessThan(discardBox.x);
  expect(discardBox.x).toBeLessThan(saveBox.x);
  expect(discardBox.height).toBeGreaterThanOrEqual(32);
  expect(saveBox.height).toBeGreaterThanOrEqual(32);

  await discard.tap();
  await save.tap();
  await expect(page.getByTestId(harness.ctSaveBarDiscards)).toHaveText('1');
  await expect(page.getByTestId(harness.ctSaveBarSaves)).toHaveText('1');
});

test('SaveBar_WhenClean_ShouldNotShow', async ({ mount, page }) => {
  await mount(<SaveBarHarness dirty={false} />);
  await expect(page.getByTestId(harness.ctSaveBarPageEnd)).toBeAttached();
  await expect(page.getByTestId(ids.saveBar)).toHaveCount(0);
});

test('SaveBar_WhenSaveDisabled_ShouldNotSave', async ({ mount, page }) => {
  await mount(<SaveBarHarness saveDisabled />);
  const save = page.getByTestId(ids.saveBarSave);
  await expect(save).toBeDisabled();
  await save.tap({ force: true });
  await expect(page.getByTestId(harness.ctSaveBarSaves)).toHaveText('0');
});

test('SaveBar_WhenSaving_ShouldDisableSave', async ({ mount, page }) => {
  await mount(<SaveBarHarness saving />);
  const save = page.getByTestId(ids.saveBarSave);
  await expect(save).toBeDisabled();
  await expect(save).toHaveText('Saving…');
});

test('SaveBar_WhenShown_ShouldLeaveRoomForThePageEnd', async ({ mount, page }) => {
  await mount(<SaveBarHarness />);
  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  const end = await page.getByTestId(harness.ctSaveBarPageEnd).boundingBox();
  const bar = await page.getByTestId(ids.saveBar).boundingBox();
  if (!end || !bar) throw new Error('the page end and the bar should render');
  expect(end.y + end.height).toBeLessThanOrEqual(bar.y);
});

test('SaveBar_WhenMessageIsAButton_ShouldBeClickable', async ({ mount, page }) => {
  await mount(<SaveBarHarness messageIsButton />);
  const button = page.getByTestId(ids.saveBarMessage).getByTestId(harness.ctSaveBarMessageButton);
  expect((await button.boundingBox())?.height).toBeGreaterThanOrEqual(32);
  await button.tap();
  await expect(page.getByTestId(harness.ctSaveBarMessageClicks)).toHaveText('1');
  await expect(page.getByTestId(harness.ctSaveBarSaves)).toHaveText('0');
});

test('SaveBar_WhenInsideAFullScreenDialog_ShouldSitAtTheDialogBottomAboveItsBody', async ({
  mount,
  page,
}) => {
  await mount(<SaveBarHarness inDialog />);
  const dialog = page.getByTestId(componentTestIds.dialog);
  const bar = dialog.getByTestId(ids.saveBar);
  await expect(bar).toBeVisible();
  const barBox = await bar.boundingBox();
  const dialogBox = await dialog.boundingBox();
  if (!barBox || !dialogBox) throw new Error('the dialog and the bar should render');
  expect(barBox.y + barBox.height).toBe(dialogBox.y + dialogBox.height);

  // The bar is on top of the body: the point at its centre hits the bar, not a body row behind it.
  const onTop = await page.evaluate(
    ({ x, y, id }) => document.elementFromPoint(x, y)?.closest(`[data-testid="${id}"]`) !== null,
    { x: barBox.x + 4, y: barBox.y + barBox.height / 2, id: ids.saveBar },
  );
  expect(onTop).toBe(true);

  const end = page.getByTestId(harness.ctSaveBarPageEnd);
  await end.scrollIntoViewIfNeeded();
  await dialog.locator('div.overflow-auto').evaluate((body) => {
    body.scrollTop = body.scrollHeight;
  });
  const endBox = await end.boundingBox();
  const barAfter = await bar.boundingBox();
  if (!endBox || !barAfter) throw new Error('the body end and the bar should render');
  expect(endBox.y + endBox.height).toBeLessThanOrEqual(barAfter.y);
  await bar.getByTestId(ids.saveBarSave).tap();
  await expect(page.getByTestId(harness.ctSaveBarSaves)).toHaveText('1');
});

for (const theme of ['light', 'dark'] as const) {
  test(`SaveBar_WhenDirty_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<SaveBarHarness messageIsButton />);
    await expect(page).toHaveScreenshot(`save-bar.${theme}.png`, { animations: 'disabled' });
  });
}
