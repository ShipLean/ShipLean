import { expect } from '@playwright/experimental-ct-react';
import { stubRetros } from '../../features/retros/ct/retroFixtures';
import { retrosTestIds as ids } from '../../features/retros/testIds';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';

// Retrospectives › Insights on a phone, the frame (SHIP-731, L731a): the tabs and the Range · ⋯ row over whatever the
// views show. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/retroInsightsFrame.phone.spec.tsx.
async function arrange(page: Page) {
  await stubShell(page);
  await stubRetros(page);
}

phonePageShots([
  {
    name: 'retro-insights-frame',
    route: '/p/SHIP/retros/insights',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.retroInsightsToolbar)).toBeVisible();
      await expect(page.getByTestId(ids.retroRecurringRow).first()).toBeVisible();
    },
  },
]);
