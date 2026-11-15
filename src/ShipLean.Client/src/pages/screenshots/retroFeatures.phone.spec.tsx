import { expect } from '@playwright/experimental-ct-react';
import { stubRetros } from '../../features/retros/ct/retroFixtures';
import { retrosTestIds as ids } from '../../features/retros/testIds';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';

// Retrospectives › Features on a phone (SHIP-728): the mockup retros-features in design/mobile/images, from the pinned fixtures.
async function arrange(page: Page) {
  await stubShell(page);
  await stubRetros(page);
}

phonePageShots([
  {
    name: 'retros-features',
    route: '/p/SHIP/retros/features',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.retroFeaturesPanel)).toBeVisible();
      await expect(page.getByTestId(ids.retroFeaturesRow).first()).toBeVisible();
    },
  },
]);
