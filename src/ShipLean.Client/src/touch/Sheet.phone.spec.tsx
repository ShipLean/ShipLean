import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { componentTestIds } from '../components/testIds';
import { SheetHarness } from './ct/SheetHarness';
import { sheetHarnessIds } from './ct/sheetHarnessIds';
import { touchTestIds } from './testIds';

const THEMES = ['light', 'dark'] as const;

/** A real touch drag (CDP), straight down by `dy` px in `steps` moves `stepMs` apart, from the centre of `testId`. */
async function swipeDown(page: Page, testId: string, dy: number, steps: number, stepMs: number) {
  const box = await page.getByTestId(testId).boundingBox();
  if (!box) throw new Error(`${testId} is not visible`);
  const x = box.x + box.width / 2;
  const y = box.y + box.height / 2;
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y }] });
  for (let step = 1; step <= steps; step++) {
    await page.waitForTimeout(stepMs);
    await cdp.send('Input.dispatchTouchEvent', {
      type: 'touchMove',
      touchPoints: [{ x, y: y + (dy * step) / steps }],
    });
  }
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  await cdp.detach();
}

test('Sheet_WhenOpen_ShouldShowGrabTitleAndCloseAndBeAtMost85dvh', async ({ mount, page }) => {
  await mount(<SheetHarness variant="basic" />);
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet).toBeVisible();
  await expect(sheet).toHaveAttribute('role', 'dialog');
  await expect(sheet).toHaveAccessibleName('Work item');
  await expect(page.getByTestId(touchTestIds.sheetGrab)).toBeVisible();
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Work item');
  await expect(page.getByTestId(touchTestIds.sheetFooter)).toBeVisible();
  const close = page.getByTestId(touchTestIds.sheetClose);
  await expect(close).toHaveText('Close');
  expect((await close.boundingBox())?.height).toBeGreaterThanOrEqual(32);

  // Title left, Close right; the sheet sits on the bottom edge, full width, and never taller than 85dvh.
  const title = await page.getByTestId(touchTestIds.sheetTitle).boundingBox();
  const closeBox = await close.boundingBox();
  expect(title!.x).toBeLessThan(closeBox!.x);
  const box = (await sheet.boundingBox())!;
  expect(box.x).toBe(0);
  expect(box.width).toBe(390);
  expect(box.y + box.height).toBeCloseTo(844, 0);
  expect(await sheet.evaluate((el) => getComputedStyle(el).maxHeight)).toBe(`${0.85 * 844}px`);
  const scrim = page.getByTestId(touchTestIds.sheetScrim);
  expect(await scrim.evaluate((el) => getComputedStyle(el).zIndex)).toBe('65');
  expect(await sheet.evaluate((el) => getComputedStyle(el).zIndex)).toBe('70');
});

test('Sheet_WhenScrimTapped_ShouldClose', async ({ mount, page }) => {
  await mount(<SheetHarness variant="basic" />);
  await page.getByTestId(touchTestIds.sheetScrim).tap({ position: { x: 195, y: 40 } });
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.sheetScrim)).toHaveCount(0);
  await expect(page.getByTestId(sheetHarnessIds.ctSheetState)).toHaveText('closed');
});

test('Sheet_WhenEscPressed_ShouldClose', async ({ mount, page }) => {
  await mount(<SheetHarness variant="basic" />);
  await expect(page.getByTestId(touchTestIds.sheet)).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

test('Sheet_WhenCloseTapped_ShouldClose', async ({ mount, page }) => {
  await mount(<SheetHarness variant="basic" />);
  await page.getByTestId(touchTestIds.sheetClose).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId(sheetHarnessIds.ctSheetState)).toHaveText('closed');
});

test('Sheet_WhenSwipedDown_ShouldClose', async ({ mount, page }) => {
  await mount(<SheetHarness variant="basic" />);
  await swipeDown(page, touchTestIds.sheetTitle, 200, 10, 16);
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId(sheetHarnessIds.ctSheetState)).toHaveText('closed');
});

test('Sheet_WhenSwipedALittle_ShouldStayOpen', async ({ mount, page }) => {
  await mount(<SheetHarness variant="basic" />);
  const sheet = page.getByTestId(touchTestIds.sheet);
  const before = (await sheet.boundingBox())!.y;
  // 30px over about 600ms: neither past the 80px threshold nor a flick.
  await swipeDown(page, touchTestIds.sheetTitle, 30, 10, 60);
  await expect(sheet).toBeVisible();
  await expect(page.getByTestId(sheetHarnessIds.ctSheetState)).toHaveText('open');
  // It snaps back to where it was.
  await expect.poll(async () => (await sheet.boundingBox())!.y).toBeCloseTo(before, 0);
});

test('Sheet_WhenItHasASearchField_ShouldNotFocusIt', async ({ mount, page }) => {
  await mount(<SheetHarness variant="search" />);
  await expect(page.getByTestId(touchTestIds.sheet)).toBeFocused();
  const search = page.getByTestId(sheetHarnessIds.ctSheetSearch);
  await expect(search).not.toBeFocused();
  // The 16px input rule (K1) reaches a sheet's field.
  expect(await search.evaluate((el) => getComputedStyle(el).fontSize)).toBe('16px');
  await search.tap();
  await expect(search).toBeFocused();
});

test('Sheet_WhenContentIsLong_ShouldScrollItsBodyOnly', async ({ mount, page }) => {
  await mount(<SheetHarness variant="long" />);
  const sheet = page.getByTestId(touchTestIds.sheet);
  const body = page.getByTestId(touchTestIds.sheetBody);
  expect((await sheet.boundingBox())!.height).toBeLessThanOrEqual(0.85 * 844 + 0.5);
  const sizes = await body.evaluate((el) => ({ scroll: el.scrollHeight, client: el.clientHeight }));
  expect(sizes.scroll).toBeGreaterThan(sizes.client);
  // The long title stays on one line.
  expect((await page.getByTestId(touchTestIds.sheetTitle).boundingBox())!.height).toBeLessThan(32);

  await body.hover();
  await page.mouse.wheel(0, 400);
  await expect.poll(() => body.evaluate((el) => el.scrollTop)).toBeGreaterThan(0);
  // The grab bar and head stay put, and the page behind does not scroll.
  await expect(page.getByTestId(touchTestIds.sheetGrab)).toBeInViewport();
  await expect(page.getByTestId(touchTestIds.sheetClose)).toBeInViewport();
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
  expect(await sheet.evaluate((el) => el.scrollTop)).toBe(0);
});

test('Sheet_WhenEscPressedInsideADialog_ShouldCloseOnlyTheSheet', async ({ mount, page }) => {
  await mount(<SheetHarness variant="dialog" />);
  await page.getByTestId(sheetHarnessIds.ctSheetDialogOpen).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId(componentTestIds.dialog)).toBeVisible();
  // A second Esc, now in the dialog, closes the dialog.
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCount(0);
});

for (const theme of THEMES) {
  test(`Sheet_Screenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<SheetHarness variant="basic" />);
    await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    await expect(page).toHaveScreenshot(`sheet.${theme}.png`, { animations: 'disabled' });
  });
}
