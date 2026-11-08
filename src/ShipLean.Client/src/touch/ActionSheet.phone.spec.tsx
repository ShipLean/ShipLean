import { expect, test } from '@playwright/experimental-ct-react';
import { SheetHarness } from './ct/SheetHarness';
import { sheetHarnessIds } from './ct/sheetHarnessIds';
import { touchTestIds } from './testIds';

const THEMES = ['light', 'dark'] as const;

test('ActionSheet_WhenActionTapped_ShouldRunItAndClose', async ({ mount, page }) => {
  await mount(<SheetHarness variant="actions" />);
  await page.locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="move"]`).tap();
  await expect(page.getByTestId(sheetHarnessIds.ctSheetLastAction)).toHaveText('move');
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId(sheetHarnessIds.ctSheetState)).toHaveText('closed');
});

test('ActionSheet_WhenActionDisabled_ShouldNotRunIt', async ({ mount, page }) => {
  await mount(<SheetHarness variant="actions" />);
  const archive = page.locator(
    `[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="archive"]`,
  );
  await expect(archive).toBeDisabled();
  await archive.tap({ force: true });
  await expect(page.getByTestId(sheetHarnessIds.ctSheetLastAction)).toHaveText('none');
  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
  // Greyed: the muted colour, not the row colour.
  const muted = await page.evaluate(() => {
    const probe = document.createElement('span');
    document.body.append(probe);
    probe.style.color = 'var(--color-muted)';
    const muted = getComputedStyle(probe).color;
    probe.remove();
    return muted;
  });
  await expect(archive).toHaveCSS('color', muted);
});

test('ActionSheet_WhenDanger_ShouldUseTheDangerColour', async ({ mount, page }) => {
  await mount(<SheetHarness variant="actions" />);
  const danger = await page.evaluate(() => {
    const probe = document.createElement('span');
    document.body.append(probe);
    probe.style.color = 'var(--color-danger)';
    const colour = getComputedStyle(probe).color;
    probe.remove();
    return colour;
  });
  const row = (id: string) =>
    page.locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${id}"]`);
  await expect(row('delete')).toHaveCSS('color', danger);
  await expect(row('copy')).not.toHaveCSS('color', danger);
});

test('ActionSheet_ShouldHaveRowsAtLeast32pxHigh', async ({ mount, page }) => {
  await mount(<SheetHarness variant="actions" />);
  const rows = page.getByTestId(touchTestIds.actionSheetRow);
  await expect(rows).toHaveCount(4);
  for (const box of await rows.evaluateAll((els) =>
    els.map((el) => el.getBoundingClientRect().toJSON()),
  )) {
    expect(box.height).toBeGreaterThanOrEqual(32);
    expect(box.width).toBeGreaterThan(300);
  }
});

test('MoreButton_WhenTapped_ShouldOpenItsActionSheet', async ({ mount, page }) => {
  await mount(<SheetHarness variant="more" />);
  const more = page.getByTestId(touchTestIds.moreButton);
  await expect(more).toHaveAccessibleName('Item actions');
  await expect(more).toHaveAttribute('aria-haspopup', 'dialog');
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await more.tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Item actions');
  await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(4);
  await page.locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="copy"]`).tap();
  await expect(page.getByTestId(sheetHarnessIds.ctSheetLastAction)).toHaveText('copy');
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

test('MoreButton_ShouldBeAtLeast32pxSquare', async ({ mount, page }) => {
  await mount(<SheetHarness variant="more" />);
  const box = (await page.getByTestId(touchTestIds.moreButton).boundingBox())!;
  expect(box.width).toBeGreaterThanOrEqual(32);
  expect(box.height).toBeGreaterThanOrEqual(32);
  expect(box.width).toBe(box.height);
});

for (const theme of THEMES) {
  test(`ActionSheet_Screenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<SheetHarness variant="actions" />);
    await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    await expect(page).toHaveScreenshot(`action-sheet.${theme}.png`, { animations: 'disabled' });
  });
}
