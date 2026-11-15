import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { attachmentTestIds as ids } from '../testIds';
import { ImageViewerHarness } from './ct/ImageViewerHarness';

test('ImageViewer_WhenThumbTapped_ShouldShowThatImage', async ({ mount, page }) => {
  await mount(<ImageViewerHarness />);
  const counter = page.getByTestId(ids.viewerCounter);
  await expect(counter).toHaveText('1 of 3 · SHIP-101');
  await expect(page.getByTestId(touchTestIds.imageViewerImage)).toHaveAttribute(
    'alt',
    'Checkout flow',
  );

  await page.getByTestId(ids.viewerThumb).nth(2).click();

  await expect(counter).toHaveText('3 of 3 · SHIP-101');
  await expect(page.getByTestId(touchTestIds.imageViewerImage)).toHaveAttribute('alt', 'Receipt');
  await expect(page.getByTestId(ids.viewerThumb).nth(2)).toHaveAttribute('aria-current', 'true');
});

test('ImageViewer_WhenMoreTapped_ShouldOfferDownloadCopyLinkOpenOriginal', async ({
  mount,
  page,
}) => {
  await mount(<ImageViewerHarness />);

  await page.getByTestId(touchTestIds.moreButton).click();

  const rows = page.getByTestId(touchTestIds.actionSheetRow);
  await expect(rows).toHaveText(['Download', 'Copy link', 'Open original']);
  await expect(rows.first()).toBeVisible();
  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
  // The sheet sits over the viewer, not behind it: its first row answers a tap.
  await rows.nth(1).click();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.imageViewer)).toBeVisible();
});

test('ImageViewer_WhenZoomed_ShouldNotSwipe', async ({ mount, page }) => {
  await mount(<ImageViewerHarness />);
  const stage = page.getByTestId(ids.viewerStage);
  const counter = page.getByTestId(ids.viewerCounter);
  const box = await stage.boundingBox();
  const y = (box?.y ?? 0) + (box?.height ?? 0) / 2;
  const left = (box?.x ?? 0) + 60;
  const right = (box?.x ?? 0) + (box?.width ?? 0) - 60;

  const drag = async (from: number, to: number) => {
    await page.mouse.move(from, y);
    await page.mouse.down();
    await page.mouse.move((from + to) / 2, y, { steps: 3 });
    await page.mouse.move(to, y, { steps: 3 });
    await page.mouse.up();
  };

  await stage.dblclick({ position: { x: 195, y: 200 } });
  await expect(stage).toHaveAttribute('data-zoomed', 'true');
  await drag(right, left);
  await expect(counter).toHaveText('1 of 3 · SHIP-101');

  await stage.dblclick({ position: { x: 195, y: 200 } });
  await expect(stage).toHaveAttribute('data-zoomed', 'false');
  await drag(right, left);
  await expect(counter).toHaveText('2 of 3 · SHIP-101');
});
