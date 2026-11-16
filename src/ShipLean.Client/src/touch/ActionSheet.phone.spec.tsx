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

// Rows with none of the K1 fields: the baselines from before K1, unchanged.
for (const theme of THEMES) {
  test(`ActionSheet_WhenRowsHaveNoAnatomy_ShouldMatchTodaysShot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<SheetHarness variant="actions" />);
    await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    await expect(page).toHaveScreenshot(`action-sheet.${theme}.png`, { animations: 'disabled' });
  });
}

test('MoreButton_WhenChildrenGiven_ShouldRenderThemBeforeTheActionRowsAndCloseOnRequest', async ({
  mount,
  page,
}) => {
  await mount(<SheetHarness variant="moreSlot" />);
  await page.getByTestId(touchTestIds.moreButton).tap();
  const slot = page.getByTestId(sheetHarnessIds.ctSheetSlot);
  await expect(slot).toBeVisible();
  const slotBox = await slot.boundingBox();
  const firstRow = await page.getByTestId(touchTestIds.actionSheetRow).first().boundingBox();
  expect(slotBox!.y + slotBox!.height).toBeLessThanOrEqual(firstRow!.y);
  await slot.tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

const richRow = (id: string) => `[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${id}"]`;

for (const theme of THEMES) {
  test(`ActionSheet_WhenRowsHaveIconHintDetail_ShouldMatchItsScreenshot_action-sheet-rich_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<SheetHarness variant="rich" />);
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet).toBeVisible();
    await expect(sheet.getByTestId(touchTestIds.actionSheetHint).first()).toHaveText('14 KB');
    await expect(page.locator(richRow('open')).getByTestId(touchTestIds.actionSheetDetail)).toHaveText(
      'full screen',
    );
    await expect(sheet).toHaveScreenshot(`action-sheet-rich.${theme}.png`, { animations: 'disabled' });
  });
}

test('ActionSheet_WhenActionsShareAGroup_ShouldDrawOneHeadingPerGroup', async ({ mount, page }) => {
  await mount(<SheetHarness variant="rich" />);
  // Three Move to rows under one heading, the toggle's View group, and About's empty group (a divider, no heading).
  await expect(page.getByTestId(touchTestIds.actionSheetGroup)).toHaveText(['Move to', 'View']);
  const body = page.getByTestId(touchTestIds.sheetBody);
  await expect(body.locator('hr')).toHaveCount(3);
});

test('ActionSheet_WhenRowIsChecked_ShouldMarkItCurrent', async ({ mount, page }) => {
  await mount(<SheetHarness variant="rich" />);
  await expect(page.locator(richRow('progress'))).toHaveAttribute('aria-current', 'true');
  await expect(page.locator(richRow('progress'))).toContainText('✓');
  await expect(page.locator(richRow('new'))).not.toHaveAttribute('aria-current');
  await expect(page.locator(richRow('new'))).not.toContainText('✓');
});

test('ActionSheet_WhenToggleTapped_ShouldRunItAndStayOpen', async ({ mount, page }) => {
  await mount(<SheetHarness variant="rich" />);
  const toggle = page.getByTestId(touchTestIds.actionSheetToggle);
  await expect(toggle).toHaveAttribute('role', 'switch');
  await expect(toggle).toHaveAttribute('aria-checked', 'false');
  // Toggle rows come after the other rows, whatever the order they were given in.
  const toggleTop = (await toggle.boundingBox())!.y;
  const aboutTop = (await page.locator(richRow('about')).boundingBox())!.y;
  expect(toggleTop).toBeGreaterThan(aboutTop);
  await toggle.tap();
  await expect(page.getByTestId(sheetHarnessIds.ctSheetLastAction)).toHaveText('tasks');
  await expect(toggle).toHaveAttribute('aria-checked', 'true');
  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
});

for (const theme of THEMES) {
  test(`MoreButton_WhenBordered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<SheetHarness variant="moreBordered" />);
    const more = page.getByTestId(touchTestIds.moreButton);
    const box = (await more.boundingBox())!;
    expect(box.width).toBeGreaterThanOrEqual(32);
    expect(box.width).toBe(box.height);
    await expect(more).toHaveScreenshot(`more-button-bordered.${theme}.png`, { animations: 'disabled' });
  });
}

test('MoreButton_WhenGivenSubtitleAndFooter_ShouldRenderBoth', async ({ mount, page }) => {
  await mount(<SheetHarness variant="moreBordered" />);
  await page.getByTestId(touchTestIds.moreButton).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Sprint 24');
  await expect(sheet).toHaveAccessibleDescription('19 Oct – 30 Oct');
  const footer = sheet.getByTestId(touchTestIds.actionSheetFooter);
  await expect(footer).toHaveText('Closing a sprint asks where unfinished work goes.');
  const lastRow = (await page.getByTestId(touchTestIds.actionSheetRow).last().boundingBox())!;
  expect((await footer.boundingBox())!.y).toBeGreaterThanOrEqual(lastRow.y + lastRow.height);
});
