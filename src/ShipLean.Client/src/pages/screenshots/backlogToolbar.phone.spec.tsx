import { expect } from '@playwright/experimental-ct-react';
import { criteriaTestIds } from '../../features/criteria/testIds';
import { backlogView, itemRow, review, ROWS } from '../../features/backlog/ct/fixtures';
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
// SHIP-160 has no points: with the check on, ⋯ More offers "Estimate what has no points".
const arrangeUnestimated = async (page: Parameters<typeof stubShell>[0]) => {
  await stubShell(page);
  await stubBacklog(page, {
    ...backlogView(ROWS),
    review: review({ unestimated: [ROWS[0].id] }),
  });
};
// Four more items the unfiltered list holds beyond the two the filter keeps.
const WHOLE_EXTRA = ['SHIP-201', 'SHIP-202', 'SHIP-203', 'SHIP-204'].map((key) =>
  itemRow({ key, points: 2 }),
);
const ready = (page: Parameters<typeof stubShell>[0]) =>
  expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length);

phonePageShots([
  { name: 'backlogs-toolbar', route, shell: true, arrange, ready },
  {
    name: 'backlogs-more',
    route,
    shell: true,
    arrange: arrangeUnestimated,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.toolbarRow).getByTestId(touchTestIds.moreButton).tap();
      await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    },
  },
  {
    name: 'backlogs-filter',
    route,
    shell: true,
    // The filtered read answers with two rows, the unfiltered one with the whole list: "Showing 2 of 6".
    arrange: async (page) => {
      await stubShell(page);
      await stubBacklog(page, backlogView([...ROWS, ...WHOLE_EXTRA]), {
        answer: (search) =>
          search.get('filter') ? { status: 200, body: backlogView(ROWS) } : undefined,
      });
    },
    ready: (page) =>
      expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length + WHOLE_EXTRA.length),
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
      await page.getByTestId(ids.toolbarRow).getByTestId(touchTestIds.moreButton).tap();
      await page
        .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="search"]`)
        .tap();
      await page.getByTestId(ids.searchBox).fill('SHIP-1');
      await expect(page.getByTestId(ids.searchBox)).toHaveValue('SHIP-1');
      await expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length);
    },
  },
]);
