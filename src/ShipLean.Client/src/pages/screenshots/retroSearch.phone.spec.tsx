import { expect } from '@playwright/experimental-ct-react';
import { stubRetros } from '../../features/retros/ct/retroFixtures';
import { retrosTestIds as ids } from '../../features/retros/testIds';
import { touchTestIds } from '../../touch/testIds';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';

// Retrospectives › Search on a phone (SHIP-730): the page as the mockups retro-search and retro-search-filter show it,
// from the pinned fixtures. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/retroSearch.phone.spec.tsx.
async function arrange(page: Page) {
  await stubShell(page);
  await stubRetros(page);
}

const ready = async (page: Page) => {
  await expect(page.getByTestId(ids.retrosSearchPanel)).toBeVisible();
  await expect(page.getByTestId(ids.retrosRow).first()).toBeVisible();
};

phonePageShots([
  { name: 'retro-search', route: '/p/SHIP/retros', shell: true, arrange, ready },
  {
    name: 'retro-search-filter',
    route: '/p/SHIP/retros?category=wentBadly',
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.retrosFilterOpen).tap();
      await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    },
  },
]);
