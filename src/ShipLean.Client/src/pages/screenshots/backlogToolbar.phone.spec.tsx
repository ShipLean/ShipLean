import { expect } from '@playwright/experimental-ct-react';
import { criteriaTestIds } from '../../features/criteria/testIds';
import { backlogView, ROWS } from '../../features/backlog/ct/fixtures';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { backlogTestIds as ids } from '../../features/backlog/testIds';
import { touchTestIds } from '../../touch/testIds';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';

// The Stories backlog's toolbar on a phone (SHIP-716 D4, D7, D10): the one row, ⋯ More, the Filter sheet and the search field.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/backlogToolbar.phone.spec.tsx.

const route = '/p/SHIP/backlogs/stories';
const arrange = async (page: Parameters<typeof stubShell>[0]) => {
  await stubShell(page);
  await stubBacklog(page, backlogView(ROWS));
};
const ready = (page: Parameters<typeof stubShell>[0]) =>
  expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length);

phonePageShots([
  { name: 'backlogs-toolbar', route, shell: true, arrange, ready },
  {
    name: 'backlogs-more',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(touchTestIds.moreButton).tap();
      await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    },
  },
  {
    name: 'backlogs-filter',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.filterButton).tap();
      await expect(page.getByTestId(criteriaTestIds.clause)).toHaveCount(1);
    },
  },
  {
    name: 'backlogs-search',
    route,
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(touchTestIds.moreButton).tap();
      await page.getByTestId(ids.moreSearch).tap();
      await page.getByTestId(ids.searchBox).fill('checkout');
      await expect(page.getByTestId(ids.searchBox)).toHaveValue('checkout');
    },
  },
]);
