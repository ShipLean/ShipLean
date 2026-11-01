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

const insightsView = (view: string, ready: string) => ({
  name: `retro-insights-${view}`,
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
    name: 'sprint-retro-by-category',
    route: '/p/SHIP/sprints/sprint-24/retro',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.sprintRetroPanel)).toBeVisible();
      await expect(page.getByTestId(ids.retroPoint).first()).toBeVisible();
      await expect(page.getByTestId(ids.retroRail)).toBeVisible();
    },
  },
  {
    name: 'sprint-retro-by-work-item',
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
    route: `/p/SHIP/retros?selected=${retroSearch.rows[0].retroId}`,
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
