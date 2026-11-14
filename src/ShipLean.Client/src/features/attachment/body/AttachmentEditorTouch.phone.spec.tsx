import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import {
  checkoutV2,
  designView,
  stubFeaturePage,
} from '../../../pages/screenshots/fixtures/feature';
import { touchTestIds } from '../../../touch/testIds';
import { attachmentTestIds as ids } from '../testIds';

// The Markdown editor on a phone (SHIP-715, L715b) on the CT phone project: its toolbar takes the top bar's pinned
// spot and the editor fills the screen under it. Mounted as the real page, so the shell's top bar is there.

const route = `/p/SHIP/items/SHIP-100/attachments/${designView.attachment.id}`;

type Fixtures = Parameters<Parameters<typeof test>[2]>[0];

async function openEditor({ mount, page }: Pick<Fixtures, 'mount' | 'page'>) {
  await stubFeaturePage(page, checkoutV2, designView);
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await page.getByTestId(ids.edit).tap();
  await expect(page.getByTestId(ids.editorInput)).toBeVisible();
}

test('AttachmentEditor_WhenTouch_ShouldPinToolbarAndHideFullscreen', async ({ mount, page }) => {
  await openEditor({ mount, page });

  const pinned = page.getByTestId(touchTestIds.topBarPinned);
  await expect(pinned).toBeVisible();
  await expect(page.getByTestId(touchTestIds.topBar)).toHaveCount(0);
  const toolbar = page.locator('.w-md-editor > .w-md-editor-toolbar');
  const box = (await toolbar.boundingBox())!;
  const slot = (await page.getByTestId(ids.editorToolbarSlot).boundingBox())!;
  expect(box.y).toBe(0);
  expect(box.height).toBeGreaterThanOrEqual(48);
  expect(box.y + box.height).toBeGreaterThanOrEqual(slot.y + slot.height);
  await expect(page.locator('.w-md-editor-toolbar button[data-name="fullscreen"]')).toHaveCount(0);
  // One row that scrolls sideways: the toolbar's content is wider than the toolbar, and it does not wrap.
  const row = await toolbar.evaluate((el) => ({
    scrollWidth: el.scrollWidth,
    clientWidth: el.clientWidth,
    overflowX: getComputedStyle(el).overflowX,
  }));
  expect(row.overflowX).toBe('auto');
  expect(row.scrollWidth).toBeGreaterThan(row.clientWidth);
  // The page scrolling leaves it where it is.
  await page.evaluate(() => window.scrollTo(0, 200));
  expect((await toolbar.boundingBox())!.y).toBe(0);
  // Leaving edit brings the ordinary bar back.
  await page.getByTestId(ids.editorInput).fill('# Changed\n');
  await page.getByTestId(touchTestIds.saveBarDiscard).tap();
  await expect(page.getByTestId(touchTestIds.topBarPinned)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.topBarBack)).toBeVisible();
});

test('AttachmentEditor_WhenTouch_ShouldFillHeightBelowTopBar', async ({ mount, page }) => {
  await openEditor({ mount, page });

  const viewport = page.viewportSize()!;
  const editor = (await page.locator('.w-md-editor').boundingBox())!;
  expect(editor.height).toBeCloseTo(viewport.height - 48, 0);
  await expect(page.getByTestId(ids.save)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
  await page.getByTestId(ids.editorInput).fill('# Changed\n');
  const bar = page.getByTestId(touchTestIds.saveBar);
  await expect(bar).toBeVisible();
  await expect(page.getByTestId(touchTestIds.saveBarMessage)).toHaveText(
    'Saving replaces the file',
  );
});
