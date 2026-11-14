import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { attachmentTestIds } from '../../attachment/testIds';
import { priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { stubComments } from '../../workItem/ct/stubDetails';
import { attachment, stubSide } from '../../workItem/ct/stubSide';
import { feature, stubFeature } from '../ct/stubFeature';
import { featureTestIds as ids } from '../testIds';
import { RailViewerHarness } from './ct/RailViewerHarness';

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

const attached = [
  { ...attachment('i1', 'checkout-funnel.png', priya, 90112), kind: 'image' as const },
  { ...attachment('i2', 'card-picker.png', priya, 80112), kind: 'image' as const },
  { ...attachment('f1', 'funnel-oct.csv', priya, 31744), kind: 'file' as const },
];

async function stubAll(page: StubbedPage) {
  await stubApi(page, feature);
  await stubSide(page, priya, attached, []);
  await stubComments(page, priya, []);
  await stubFeature(page);
}

test('AttachmentsRail_WhenOnAPhone_ShouldMakeAttachATouchTarget', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<RailViewerHarness itemKey="SHIP-100" />, route);

  const box = await screen.getByTestId(ids.railAttach).boundingBox();
  expect(box?.height).toBeGreaterThanOrEqual(32);
});

test('AttachmentsRail_WhenImageTappedOnAPhone_ShouldOpenTheViewerOnItsImages', async ({
  mount,
  page,
}) => {
  await stubAll(page);
  const screen = await mount(<RailViewerHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.railItem).filter({ hasText: 'card-picker.png' }).click();

  await expect(page.getByTestId(touchTestIds.imageViewer)).toBeVisible();
  await expect(page.getByTestId(attachmentTestIds.viewerCounter)).toHaveText('2 of 2 · SHIP-100');
  await expect(page.getByTestId(ids.imagePreview)).toHaveCount(0);
});
