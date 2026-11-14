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
  // One row that scrolls sideways: every command button shares one top, is at least 32px, and the row is wider
  // than the toolbar.
  const buttons = toolbar.locator('> ul > li > button');
  const tops = await buttons.evaluateAll((all) =>
    all.map((el) => {
      const r = el.getBoundingClientRect();
      return { top: Math.round(r.top), height: r.height, width: r.width };
    }),
  );
  expect(tops.length).toBeGreaterThan(8);
  expect(new Set(tops.map((t) => t.top)).size, JSON.stringify(tops)).toBe(1);
  for (const t of tops) {
    expect(t.height).toBeGreaterThanOrEqual(32);
    expect(t.width).toBeGreaterThanOrEqual(32);
  }
  const row = await toolbar.evaluate((el) => ({
    scrollWidth: el.scrollWidth,
    clientWidth: el.clientWidth,
  }));
  expect(row.scrollWidth).toBeGreaterThan(row.clientWidth);
  expect(await toolbar.evaluate((el) => getComputedStyle(el).overflowX)).toBe('auto');
  // The commands past the screen edge are reachable: scrolling the bar to its end brings the last one into view.
  const last = buttons.last();
  const width = page.viewportSize()!.width;
  expect((await last.boundingBox())!.x + (await last.boundingBox())!.width).toBeGreaterThan(width);
  // A real wheel scroll, which overflow-x: hidden refuses (a script's scrollLeft would not tell).
  const bar = (await toolbar.boundingBox())!;
  await page.mouse.move(bar.x + bar.width / 2, bar.y + bar.height / 2);
  await page.mouse.wheel(2000, 0);
  await expect
    .poll(async () => {
      const box = (await last.boundingBox())!;
      return box.x >= 0 && box.x + box.width <= width;
    })
    .toBe(true);
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

test('AttachmentEditor_WhenHeadingListOpenedOnTouch_ShouldListBelowTheBarInTouchRows', async ({
  mount,
  page,
}) => {
  await openEditor({ mount, page });
  const toolbar = page.locator('.w-md-editor > .w-md-editor-toolbar');
  const bar = (await toolbar.boundingBox())!;

  await toolbar.locator('> ul > li > button[data-name="title"]').tap();

  const list = page.locator('.w-md-editor-toolbar-child.active');
  await expect(list).toBeVisible();
  const box = (await list.boundingBox())!;
  expect(box.y).toBeGreaterThanOrEqual(bar.y + bar.height);
  // The list is on top there, not clipped by the bar: the element at its middle is one of its rows.
  const hit = await page.evaluate(
    ({ x, y }) => document.elementFromPoint(x, y)?.closest('.w-md-editor-toolbar-child') !== null,
    { x: box.x + box.width / 2, y: box.y + box.height / 2 },
  );
  expect(hit).toBe(true);
  // The main toolbar stays where it was, and its commands stay visible.
  expect(await toolbar.boundingBox()).toEqual(bar);
  await expect(toolbar.locator('> ul > li > button[data-name="title"]')).toBeVisible();
  const rows = list.locator('button');
  expect(await rows.count()).toBe(6);
  for (const row of await rows.all()) {
    expect((await row.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }
});
