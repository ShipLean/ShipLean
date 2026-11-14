import { expect } from '@playwright/experimental-ct-react';
import { stubRetros } from '../../features/retros/ct/retroFixtures';
import { retrosTestIds as ids } from '../../features/retros/testIds';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';

// Retrospectives › Insights, Action items and Changes & impact, as whole pages on a phone (SHIP-731, L731c), from the
// pinned fixtures. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/retroInsightsActions.phone.spec.tsx.
async function arrange(page: Page) {
  await stubShell(page);
  await stubRetros(page);
  await page.route(
    (url) => /\/api\/projects\/[^/]+\/sprints$/.test(url.pathname),
    (r) =>
      r.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'sprint-25', name: 'Sprint 25', status: 'future', startDate: '2026-11-02' },
        ]),
      }),
  );
}

phonePageShots([
  {
    name: 'retro-insights-actions',
    route: '/p/SHIP/retros/insights?view=actions',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.retroActionCard).first()).toBeVisible();
    },
  },
  {
    name: 'retro-insights-impact',
    route: '/p/SHIP/retros/insights?view=impact',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.retroImpactCard).first()).toBeVisible();
    },
  },
]);
