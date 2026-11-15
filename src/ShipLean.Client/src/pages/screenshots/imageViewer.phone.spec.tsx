import { expect } from '@playwright/experimental-ct-react';
import { featureTestIds } from '../../features/feature/testIds';
import { attachmentTestIds as ids } from '../../features/attachment/testIds';
import { touchTestIds } from '../../touch/testIds';
import { attachments, checkoutV2, designView, stubFeaturePage } from './fixtures/feature';
import { phonePageShots } from './shootPhone';
import type { Page } from './shoot';

// The full-screen image viewer and the Source view of a Markdown attachment on a phone (SHIP-715, L715c). The viewer is
// always dark, so both themes are shot to prove it. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/imageViewer.phone.spec.tsx.

const route = `/p/SHIP/items/SHIP-100/attachments/${designView.attachment.id}`;
const flow = attachments.find((file) => file.id === 'a-flow');
const images = flow
  ? [
      flow,
      {
        ...flow,
        id: 'a-picker',
        name: 'card-picker.svg',
        downloadUrl: '/api/attachments/a-picker/content',
      },
      {
        ...flow,
        id: 'a-receipt',
        name: 'receipt.svg',
        downloadUrl: '/api/attachments/a-receipt/content',
      },
    ]
  : [];

const arrange = async (page: Page) => {
  await stubFeaturePage(page, checkoutV2, designView);
  // The item has three images, so the viewer opens on the tapped one among them.
  await page.route('**/api/work-items/*/attachments', (stub) =>
    stub.request().method() === 'GET'
      ? stub.fulfill({ json: [...attachments.filter((file) => file.id !== 'a-flow'), ...images] })
      : stub.fallback(),
  );
};

// The page is reached from the item's Attachments rail, so the item's images are already loaded when the image is tapped.
const itemRoute = '/p/SHIP/items/SHIP-100';
const railReady = async (page: Page) => {
  await expect(page.getByTestId(featureTestIds.railItem)).toHaveCount(6);
};
const openFromRail = async (page: Page) => {
  await page
    .getByTestId(featureTestIds.railItem)
    .filter({ hasText: 'design-v0.2.md' })
    .getByRole('link')
    .tap();
  await expect(page.getByTestId(ids.body)).toBeVisible();
};
const opened = async (page: Page) => {
  await openFromRail(page);
  await page.locator('figure[data-md-image=attached] img').tap();
  await expect(page.getByTestId(touchTestIds.imageViewer)).toBeVisible();
  await expect(page.getByTestId(ids.viewerThumb)).toHaveCount(3);
};

phonePageShots([
  {
    name: 'viewer',
    route: itemRoute,
    shell: true,
    arrange,
    ready: railReady,
    act: opened,
  },
  {
    name: 'viewer-zoom',
    route: itemRoute,
    shell: true,
    arrange,
    ready: railReady,
    act: async (page) => {
      await opened(page);
      await page.getByTestId(ids.viewerStage).dblclick({ position: { x: 100, y: 200 } });
      await expect(page.getByTestId(ids.viewerStage)).toHaveAttribute('data-zoomed', 'true');
    },
  },
  {
    name: 'attachment-source',
    route,
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.name)).toHaveText('design-v0.2.md');
    },
    act: async (page) => {
      await page.getByTestId(ids.source).tap();
      await expect(page.getByTestId(ids.sourceText)).toBeVisible();
    },
  },
]);
