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
