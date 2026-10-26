import { expect } from '@playwright/experimental-ct-react';
import {
  backlogView,
  planningSprints,
  planningView,
  ROWS,
  treeRows,
  treeView,
} from '../../features/backlog/ct/fixtures';
import { EPIC_ROWS, FEATURE_ROWS, levelView } from '../../features/backlog/ct/levelFixtures';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { stubLevel } from '../../features/backlog/ct/stubLevels';
import { backlogTestIds } from '../../features/backlog/testIds';
import { shellTestIds } from '../../features/shell/testIds';
import { pageShots } from './shoot';
import { stubShell } from './shellStub';

const { rows: TREE_ROWS } = treeRows();

pageShots([
  {
    name: 'backlog',
    route: '/p/SHIP/backlogs/stories',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBacklog(page, backlogView(ROWS));
    },
    ready: (page) => expect(page.getByTestId(backlogTestIds.row)).toHaveCount(ROWS.length),
  },
  {
    name: 'backlog-tree',
    route: '/p/SHIP/backlogs/stories?mode=tree',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBacklog(page, treeView(TREE_ROWS));
    },
    ready: (page) => expect(page.getByTestId(backlogTestIds.treeGroup).first()).toBeVisible(),
  },
  {
    name: 'features',
    route: '/p/SHIP/backlogs/features',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubLevel(page, levelView('features', FEATURE_ROWS));
    },
    ready: (page) => expect(page.getByTestId(backlogTestIds.row)).toHaveCount(FEATURE_ROWS.length),
  },
  {
    name: 'features-epics',
    route: '/p/SHIP/backlogs/epics',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubLevel(page, levelView('epics', EPIC_ROWS));
    },
    ready: (page) => expect(page.getByTestId(backlogTestIds.row)).toHaveCount(EPIC_ROWS.length),
  },
  {
    name: 'planning',
    route: '/p/SHIP/backlogs/stories?mode=planning',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBacklog(page, planningView(), { sprints: planningSprints() });
    },
    ready: (page) => expect(page.getByTestId(backlogTestIds.planningSection)).toHaveCount(4),
  },
  {
    name: 'user-menu',
    route: '/p/SHIP/backlogs/stories',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBacklog(page, backlogView(ROWS));
    },
    ready: (page) => expect(page.getByTestId(backlogTestIds.row)).toHaveCount(ROWS.length),
    act: async (page) => {
      await page.getByTestId(shellTestIds.userButton).click();
      await expect(page.getByTestId(shellTestIds.userMenu)).toBeVisible();
    },
  },
]);
