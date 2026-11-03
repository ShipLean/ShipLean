import { expect } from '@playwright/experimental-ct-react';
import { retrosTestIds as ids } from '../../features/retros/testIds';
import { retroSearch, stubRetros } from '../../features/retros/ct/retroFixtures';
import { retroSharedTestIds } from '../../features/retroShared/testIds';
import { pageShots, type Page } from './shoot';
import { stubShell } from './shellStub';

// Retrospectives: the sprint Retro tab (both views), Search and the three Insights views, from the pinned fixtures.
async function arrange(page: Page) {
  await stubShell(page);
  await stubRetros(page);
}

// Owner P1: the six retro-insights baselines (themes, actions, impact; light and dark) are stale: stubRetros serves a pending merge proposal, which the themes view now shows in flow; regenerate them in the CI Docker image.
const insightsView = (view: string, ready: string) => ({
  // The mockups: retro-insights (themes), retro-insights-actions and retro-insights-impact.
  name: view === 'themes' ? 'retro-insights' : `retro-insights-${view}`,
  route: `/p/SHIP/retros/insights?view=${view}`,
  shell: true,
  arrange,
  ready: async (page: Page) => {
    await expect(page.getByTestId(ids.retroInsightsPanel)).toBeVisible();
    await expect(page.getByTestId(ready).first()).toBeVisible();
  },
});

pageShots([
  {
    name: 'sprint-retro',
    route: '/p/SHIP/sprints/sprint-24/retro',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.sprintRetroPanel)).toBeVisible();
      await expect(page.getByTestId(retroSharedTestIds.retroNote).first()).toBeVisible();
      await expect(page.getByTestId(ids.retroRail)).toBeVisible();
    },
  },
  {
    name: 'sprint-retro-items',
    route: '/p/SHIP/sprints/sprint-24/retro',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.retroViewByItem)).toBeVisible();
    },
    act: async (page) => {
      await page.getByTestId(ids.retroViewByItem).click();
      await expect(page.getByTestId(ids.retroCardList)).toBeVisible();
      await expect(page.getByTestId(retroSharedTestIds.retroCard).first()).toBeVisible();
    },
  },
  {
    name: 'retros-search',
    route: `/p/SHIP/retros?q=screenshot&selected=${retroSearch.rows[0].retroId}`,
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.retrosSearchPanel)).toBeVisible();
      await expect(page.getByTestId(ids.retrosRow).first()).toBeVisible();
      await expect(page.getByTestId(ids.retrosSelected)).toBeVisible();
    },
  },
  insightsView('themes', ids.retroRecurringRow),
  insightsView('actions', ids.retroActionRow),
  insightsView('impact', ids.retroImpactCard),
]);
