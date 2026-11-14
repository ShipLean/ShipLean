import { expect } from '@playwright/experimental-ct-react';
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

// A tap before the item's images have loaded opens the viewer on the page's own images only, so tap again until the
// full set is there.
const opened = async (page: Page) => {
  await expect(page.getByTestId(ids.body)).toBeVisible();
  await expect(async () => {
    const close = page.getByTestId(touchTestIds.imageViewerClose);
    if (await close.isVisible()) await close.tap();
    await page.locator('figure[data-md-image=attached] img').tap();
    await expect(page.getByTestId(touchTestIds.imageViewer)).toBeVisible({ timeout: 1000 });
    await expect(page.getByTestId(ids.viewerThumb)).toHaveCount(3, { timeout: 500 });
  }).toPass();
  await expect(page.getByTestId(touchTestIds.imageViewerImage)).toBeVisible();
};

phonePageShots([
  {
    name: 'viewer',
    route,
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.name)).toHaveText('design-v0.2.md');
    },
    act: opened,
  },
  {
    name: 'viewer-zoom',
    route,
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.name)).toHaveText('design-v0.2.md');
    },
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
