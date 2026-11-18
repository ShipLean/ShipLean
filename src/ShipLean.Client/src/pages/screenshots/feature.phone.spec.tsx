import { expect } from '@playwright/experimental-ct-react';
import { featureTestIds } from '../../features/feature/testIds';
import { touchTestIds } from '../../touch/testIds';
import { workItemTestIds } from '../../features/workItem/testIds';
import { checkoutV2, stubFeaturePage } from './fixtures/feature';
import { PEOPLE } from './shellStub';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import type { WorkItemDto } from '@shiplean/core/api/workItems';

// The whole Feature page on a phone (SHIP-714, SHIP-1626): the status band with Health "At risk", the fields, Stories and
// bugs, links, log and the rest, inside the shell; and the status editor open with a change made, so the Save bar shows.
// The phone twin of feature.spec.tsx. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/feature.phone.spec.tsx.

const atRisk: WorkItemDto = {
  ...checkoutV2,
  health: 'atRisk',
  healthSet: { at: '2026-10-24T09:12:00+00:00', atLabel: '24 Oct', by: PEOPLE.priya, agent: null },
};

const route = '/p/SHIP/items/SHIP-100';

const arrange = async (page: Page) => {
  await stubFeaturePage(page, atRisk);
  // The profile photos the people carry: the band's "Set by" avatar would otherwise draw broken.
  await page.route('**/api/users/*/photo*', (r) =>
    r.fulfill({
      status: 200,
      contentType: 'image/svg+xml',
      body: '<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><rect width="64" height="64" fill="#2f7fd8"/></svg>',
    }),
  );
};

const ready = async (page: Page) => {
  await expect(page.getByTestId(featureTestIds.statusBand)).toBeVisible();
  await expect(page.getByTestId(featureTestIds.statusHealthButton)).toContainText('At risk');
  await expect(page.getByTestId(featureTestIds.linkCard).first()).toBeVisible();
  await expect(page.getByTestId(featureTestIds.logCount)).toHaveText('3');
  // The attached image has loaded, so the baseline never catches it half drawn.
  await expect(page.getByTestId(workItemTestIds.description).locator('img')).toHaveJSProperty(
    'complete',
    true,
  );
  await expect(page.getByTestId(workItemTestIds.history)).toBeVisible();
};

phonePageShots([
  { name: 'feature', route, shell: true, arrange, ready },
  {
    name: 'feature-status-edit',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(featureTestIds.statusEdit).tap();
      await expect(page.getByTestId(featureTestIds.statusEditor)).toBeVisible();
      // One field changed, so the Save bar shows.
      await page
        .getByTestId(featureTestIds.statusEditLine)
        .fill('Saved cards are in review; the vault rate limit is the open risk.');
      await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
    },
  },
]);
