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
  // The mockups: retro-insights (themes), retro-insights-actions and retro-insights-impact.
  name: view === 'themes' ? 'retro-insights' : `retro-insights-${view}`,
  route:
    view === 'weeks' ? '/p/SHIP/retros/insights?range=w6' : `/p/SHIP/retros/insights?view=${view}`,
  shell: true,
  arrange,
  ready: async (page: Page) => {
    await expect(page.getByTestId(ids.retroInsightsPanel)).toBeVisible();
    await expect(page.getByTestId(ready).first()).toBeVisible();
  },
});

const itemReady = async (page: Page) => {
  await expect(page.getByTestId(ids.retroItemPanel)).toBeVisible();
  await expect(page.getByTestId(retroSharedTestIds.retroNote).first()).toBeVisible();
  await expect(page.getByTestId(ids.retroItemChildRow).first()).toBeVisible();
};

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
    name: 'sprint-retro-decide',
    route: '/p/SHIP/sprints/sprint-24/retro?decide=open',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.sprintRetroPanel)).toBeVisible();
      await expect(page.getByTestId(ids.retroToDecideFilter)).toBeVisible();
      await expect(page.getByTestId(retroSharedTestIds.retroNote).first()).toBeVisible();
    },
  },
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
  {
    name: 'retro-item',
    route: '/p/SHIP/retros/items/SHIP-101',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.retroItemPanel)).toBeVisible();
      await expect(page.getByTestId(retroSharedTestIds.retroNote).first()).toBeVisible();
    },
  },
  {
    name: 'retro-item-editing',
    route: '/p/SHIP/retros/items/SHIP-101',
    shell: true,
    arrange,
    ready: itemReady,
    act: async (page) => {
      await page
        .locator(
          `[data-testid="${retroSharedTestIds.retroBoardColumn}"][data-category="wentBadly"]`,
        )
        .getByTestId(retroSharedTestIds.retroBoardAdd)
        .click();
      await expect(page.getByTestId(retroSharedTestIds.retroBoardAddText)).toBeVisible();
      await expect(page.getByTestId(retroSharedTestIds.retroBoardAddCause)).toBeVisible();
    },
  },
  {
    name: 'retro-item-new-action',
    route: '/p/SHIP/retros/items/SHIP-101',
    shell: true,
    arrange,
    ready: itemReady,
    act: async (page) => {
      await page.getByTestId(ids.retroItemAddAction).click();
      await expect(page.getByTestId(retroSharedTestIds.retroActionDialog)).toBeVisible();
      await expect(page.getByTestId(retroSharedTestIds.retroActionTitle)).toBeVisible();
    },
  },
  insightsView('weeks', ids.retroRecurringRow),
  {
    name: 'retro-insights-status-tip',
    route: '/p/SHIP/retros/insights',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.retroInsightsPanel)).toBeVisible();
      await expect(page.getByTestId(ids.retroRecurringRow).first()).toBeVisible();
    },
    act: async (page) => {
      await page.getByTestId(ids.retroTipStatusHead).getByText('Status').focus();
      await expect(page.getByRole('tooltip')).toBeVisible();
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
