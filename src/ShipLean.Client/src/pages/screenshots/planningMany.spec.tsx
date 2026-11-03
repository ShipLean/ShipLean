import { expect } from '@playwright/experimental-ct-react';
import {
  planningManyView,
  planningSprints,
  backlogView,
  ROWS,
} from '../../features/backlog/ct/fixtures';
import { stubBacklog, type BacklogServer } from '../../features/backlog/ct/stubApi';
import { backlogTestIds as ids } from '../../features/backlog/testIds';
import { pageShots, type Page } from './shoot';
import { stubShell } from './shellStub';

// The SHIP-255 states: rows selected with the bar, the Undo toast under the bar, the changed-row report, the paste
// review, the Before you plan strip and its queue, and the key list. The pull line is in the `planning` shot.

const PLANNING = '/p/SHIP/backlogs/stories?mode=planning';
let server: BacklogServer;

async function arrangePlanning(page: Page) {
  await stubShell(page);
  server = await stubBacklog(page, planningManyView(), { sprints: planningSprints() });
}

const rowsReady = (page: Page) => expect(page.getByTestId(ids.planningSection)).toHaveCount(3);

async function select(page: Page, keys: string[]) {
  for (const key of keys) {
    await page.getByTestId(ids.row).filter({ hasText: key }).getByTestId(ids.rowSelect).click();
  }
  await expect(page.getByTestId(ids.bulkBar)).toBeVisible();
}

pageShots([
  {
    name: 'bulk-selected',
    route: PLANNING,
    shell: true,
    arrange: arrangePlanning,
    ready: rowsReady,
    act: (page) => select(page, ['SHIP-181', 'SHIP-182', 'SHIP-183']),
  },
  {
    name: 'bulk-undo',
    route: PLANNING,
    shell: true,
    arrange: arrangePlanning,
    ready: rowsReady,
    act: async (page) => {
      await select(page, ['SHIP-181', 'SHIP-182']);
      await page.getByTestId(ids.bulkMoveTop).click();
      await expect(page.getByTestId(ids.undo)).toBeVisible();
    },
  },
  {
    name: 'bulk-changed',
    route: PLANNING,
    shell: true,
    arrange: arrangePlanning,
    ready: rowsReady,
    act: async (page) => {
      server.staleOnBulk(['id-SHIP-182']);
      await select(page, ['SHIP-181', 'SHIP-182', 'SHIP-183']);
      await page.getByTestId(ids.bulkMoveTop).click();
      await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
    },
  },
  {
    name: 'paste-review',
    route: '/p/SHIP/backlogs/stories',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBacklog(page, backlogView(ROWS));
    },
    ready: (page) => expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length),
    act: async (page) => {
      await page.getByTestId(ids.newItem).click();
      await expect(page.getByTestId(ids.addTitle)).toBeFocused();
      await page.getByTestId(ids.addTitle).evaluate((input, value) => {
        const data = new DataTransfer();
        data.setData('text', value);
        input.dispatchEvent(
          new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }),
        );
      }, 'Export a board\n  Choose columns\n  Pick a format\nbug: Export hangs (3 pts)\nTidy the filters');
      await expect(page.getByTestId(ids.pasteRow)).toHaveCount(5);
    },
  },
  {
    name: 'plan-strip',
    route: PLANNING,
    shell: true,
    arrange: arrangePlanning,
    ready: (page) => expect(page.getByTestId(ids.planStrip)).toBeVisible(),
  },
  {
    name: 'plan-queue',
    route: PLANNING,
    shell: true,
    arrange: arrangePlanning,
    ready: rowsReady,
    act: async (page) => {
      await page.getByTestId(ids.planCheck).and(page.locator('[data-check="unestimated"]')).click();
      await expect(page.getByTestId(ids.planQueueBar)).toBeVisible();
    },
  },
  {
    name: 'keys',
    route: '/p/SHIP/backlogs/stories',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBacklog(page, backlogView(ROWS));
    },
    ready: (page) => expect(page.getByTestId(ids.row)).toHaveCount(ROWS.length),
    act: async (page) => {
      await page.getByTestId(ids.keysButton).click();
      await expect(page.getByTestId(ids.keysDialog)).toBeVisible();
    },
  },
]);
