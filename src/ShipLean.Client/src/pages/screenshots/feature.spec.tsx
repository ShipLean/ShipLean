import { expect } from '@playwright/experimental-ct-react';
import { attachmentTestIds } from '../../features/attachment/testIds';
import { featureTestIds } from '../../features/feature/testIds';
import { workItemTestIds } from '../../features/workItem/testIds';
import { checkoutV2, designView, staleCheckoutV2, stubFeaturePage } from './fixtures/feature';
import { pageShots, type Page } from './shoot';
import type { WorkItemDto } from '../../api/workItems';

const featureReady = async (page: Page) => {
  await expect(page.getByTestId(featureTestIds.statusBand)).toBeVisible();
  await expect(page.getByTestId(featureTestIds.linkCard).first()).toBeVisible();
  await expect(page.getByTestId(featureTestIds.railItem)).toHaveCount(4);
  await expect(page.getByTestId(featureTestIds.logCount)).toHaveText('3');
  // The attached image has loaded, so the baseline never catches it half drawn.
  await expect(page.getByTestId(workItemTestIds.description).locator('img')).toHaveJSProperty(
    'complete',
    true,
  );
  await expect(page.getByTestId(workItemTestIds.history)).toBeVisible();
};

const arrange = (item: WorkItemDto) => (page: Page) => stubFeaturePage(page, item);

pageShots([
  {
    name: 'feature',
    route: '/p/SHIP/items/SHIP-100',
    shell: true,
    arrange: arrange(checkoutV2),
    ready: featureReady,
  },
  {
    name: 'feature-stale',
    route: '/p/SHIP/items/SHIP-100',
    shell: true,
    arrange: arrange(staleCheckoutV2),
    ready: featureReady,
  },
  {
    name: 'feature-status-edit',
    route: '/p/SHIP/items/SHIP-100',
    shell: true,
    arrange: arrange(checkoutV2),
    ready: featureReady,
    act: async (page) => {
      await page.getByTestId(featureTestIds.statusEdit).click();
      await expect(page.getByTestId(featureTestIds.statusEditor)).toBeVisible();
    },
  },
  {
    name: 'attachment',
    route: `/p/SHIP/items/SHIP-100/attachments/${designView.attachment.id}`,
    shell: true,
    arrange: async (page) => {
      await stubFeaturePage(page, checkoutV2, designView);
    },
    ready: async (page) => {
      await expect(page.getByTestId(attachmentTestIds.name)).toHaveText('design-v0.2.md');
      await expect(page.getByTestId(attachmentTestIds.body)).toBeVisible();
      await expect(page.getByTestId(attachmentTestIds.body).locator('img')).toHaveJSProperty(
        'complete',
        true,
      );
    },
  },
]);
