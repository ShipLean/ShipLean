import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { attachmentTestIds as ids } from '../testIds';
import { AttachmentBodyHarness } from './ct/AttachmentBodyHarness';

test('AttachmentBody_WhenImageTappedWithItemImagesCached_ShouldOpenViewerOnAllOfThem', async ({
  mount,
  page,
}) => {
  // The network never answers: only the cache can supply the item's images.
  await page.route('**/api/**', () => undefined);
  await mount(<AttachmentBodyHarness />, { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } });

  await page.locator('figure[data-md-image=attached] img').tap();

  await expect(page.getByTestId(touchTestIds.imageViewer)).toBeVisible();
  await expect(page.getByTestId(ids.viewerCounter)).toHaveText('2 of 3 · SHIP-100');
});

test('AttachmentBody_WhenImageTappedBeforeItemImagesLoad_ShouldSwitchToAllOfThemOnTheTappedImage', async ({
  mount,
  page,
}) => {
  // The item's images response is held until the test releases it, so the tap comes first.
  let release: () => void = () => undefined;
  const released = new Promise<void>((resolve) => {
    release = resolve;
  });
  const asked = page.waitForRequest('**/api/work-items/w1/attachments');
  await page.route('**/api/work-items/w1/attachments', async (route) => {
    await released;
    await route.fulfill({
      json: ['a', 'b', 'c'].map((id) => ({
        id,
        name: `${id}.png`,
        kind: 'image',
        downloadUrl: `/api/attachments/${id}/content`,
      })),
    });
  });
  await mount(<AttachmentBodyHarness seedImages={false} />, {
    hooksConfig: { route: '/p/SHIP/items/SHIP-100' },
  });
  await asked;

  await page.locator('figure[data-md-image=attached] img').tap();
  await expect(page.getByTestId(touchTestIds.imageViewer)).toBeVisible();
  await expect(page.getByTestId(ids.viewerCounter)).toHaveText('1 of 1 · SHIP-100');

  release();

  await expect(page.getByTestId(ids.viewerCounter)).toHaveText('2 of 3 · SHIP-100');
});
