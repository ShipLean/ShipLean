import { expect } from '@playwright/experimental-ct-react';
import searchView from '../../../../ShipLean.TestSupport/DtoFixtures/search/search-view.json' with { type: 'json' };
import { meDto } from '../../features/auth/authFixtures';
import { backlogView, ROWS } from '../../features/backlog/ct/fixtures';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { backlogTestIds } from '../../features/backlog/testIds';
import { quickSearchTestIds } from '../../features/quickSearch/testIds';
import { pageShots, type Page } from './shoot';
import { stubShell } from './shellStub';

const recent = ['SHIP-12', 'SHIP-31', 'SHIP-48'].map((key, at) => ({
  key,
  title: ['Export a board to CSV', 'Fix the nightly job', 'Quarterly numbers'][at],
  type: ['story', 'bug', 'feature'][at],
  state: ['Active', 'New', 'Closed'][at],
  stateCategory: ['inProgress', 'proposed', 'completed'][at],
  parentPath: [],
  projectKey: 'SHIP',
}));

async function arrange(page: Page) {
  await stubShell(page);
  await stubBacklog(page, backlogView(ROWS));
  await page.route('**/api/search*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(searchView),
    }),
  );
}

// A key pressed before the shell has loaded can beat its listener, so the shots wait for the backlog rows first.
const rowsReady = (page: Page) =>
  expect(page.getByTestId(backlogTestIds.row)).toHaveCount(ROWS.length);

pageShots([
  {
    name: 'quick-search-results',
    route: '/p/SHIP/backlogs/stories',
    shell: true,
    arrange,
    ready: rowsReady,
    act: async (page) => {
      await page.keyboard.press('Control+p');
      await page.getByTestId(quickSearchTestIds.input).fill('export');
      await expect(page.getByTestId(quickSearchTestIds.option).first()).toBeVisible();
      await expect(
        page.getByTestId(quickSearchTestIds.group).and(page.locator('[data-group="items"]')),
      ).toBeVisible();
    },
  },
  {
    name: 'quick-search-empty',
    route: '/p/SHIP/backlogs/stories',
    shell: true,
    arrange,
    ready: rowsReady,
    act: async (page) => {
      // The recent list is read from storage each time the dialog opens, so seeding it here is early enough.
      await page.evaluate(
        ([key, value]) => window.localStorage.setItem(key, value),
        [`shiplean.quickSearch.recent.${meDto().id}`, JSON.stringify(recent)],
      );
      await page.keyboard.press('Control+p');
      await expect(
        page.getByTestId(quickSearchTestIds.group).and(page.locator('[data-group="recent"]')),
      ).toBeVisible();
    },
  },
]);
