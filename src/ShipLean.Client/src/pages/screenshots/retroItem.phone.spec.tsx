import { expect } from '@playwright/experimental-ct-react';
import { stubRetros } from '../../features/retros/ct/retroFixtures';
import { retrosTestIds as ids } from '../../features/retros/testIds';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';

// One item's retros on a phone (SHIP-729): a Feature's page, from the pinned fixtures. There is no mockup for this page; it
// is built from SHIP-805 and the nearest designed patterns.
async function arrange(page: Page) {
  await stubShell(page);
  await stubRetros(page);
}

phonePageShots([
  {
    name: 'retro-item',
    route: '/p/SHIP/retros/items/SHIP-101',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.retroItemPanel)).toBeVisible();
      await expect(page.getByTestId(ids.retroItemKpiToDecide)).toBeVisible();
    },
  },
]);
