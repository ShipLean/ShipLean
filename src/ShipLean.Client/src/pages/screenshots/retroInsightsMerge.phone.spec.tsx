import { expect } from '@playwright/experimental-ct-react';
import { stubRetros } from '../../features/retros/ct/retroFixtures';
import { retrosTestIds as ids } from '../../features/retros/testIds';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';

// Merge themes on a phone, full screen with a theme ticked (SHIP-731, L731d). Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/retroInsightsMerge.phone.spec.tsx.
async function arrange(page: Page) {
  await stubShell(page);
  await stubRetros(page);
}

phonePageShots([
  {
    name: 'retro-insights-merge',
    route: '/p/SHIP/retros/insights?merge=',
    shell: false,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.retroMergeDialog)).toBeVisible();
      await expect(page.getByTestId(ids.retroMergeSource).first()).toBeVisible();
    },
  },
]);
