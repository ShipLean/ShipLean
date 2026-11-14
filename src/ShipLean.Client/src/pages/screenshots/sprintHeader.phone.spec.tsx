import type { Page } from 'playwright';
import { expect } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { sprintHeaderTestIds as headerIds } from '../../features/sprintHeader/testIds';
import { sprintsTestIds as ids } from '../../features/sprints/testIds';
import { touchTestIds } from '../../touch/testIds';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';
import {
  SPRINT_24,
  sprintsAtEnd,
  sprintsWithGoals,
  stubSprintBacklog,
  stubSprintEnd,
} from './fixtures/sprints';

// The shared sprint header on a phone (SHIP-724): its picker sheet, ⋯ and the three edit flows, on the Sprint backlog.

const backlog = {
  route: `/p/SHIP/sprints/${SPRINT_24}/backlog`,
  shell: true,
  arrange: async (page: Page) => {
    await stubShell(page, { sprints: sprintsWithGoals });
    await stubSprintBacklog(page);
  },
  ready: async (page: Page) => {
    await expect(page.getByTestId(headerIds.goal)).not.toHaveText('');
    await expect(page.getByTestId(ids.storyRow).first()).toBeVisible();
  },
};

const more = async (page: Page, actionId: string) => {
  await page.getByTestId(headerIds.header).getByTestId(touchTestIds.moreButton).tap();
  await page
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${actionId}"]`)
    .tap();
};

phonePageShots([
  {
    name: 'sprint-backlog-picker',
    ...backlog,
    act: async (page) => {
      await page.getByTestId(headerIds.picker).tap();
      await expect(page.getByTestId(headerIds.pickerSheetTick)).toBeVisible();
    },
  },
  {
    name: 'sprint-backlog-more',
    ...backlog,
    act: async (page) => {
      await page.getByTestId(headerIds.header).getByTestId(touchTestIds.moreButton).tap();
      await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(4);
    },
  },
  {
    name: 'sprint-backlog-goal',
    ...backlog,
    act: async (page) => {
      await more(page, 'edit-goal');
      await page
        .getByTestId(ids.goalInput)
        .fill(
          'Customers can check out with a saved card; passkey sign-in behind a flag. Order emails go out from the new template.',
        );
      await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
      await page.getByTestId(ids.goalInput).blur();
    },
  },
  {
    name: 'sprint-backlog-edit',
    ...backlog,
    act: async (page) => {
      await more(page, 'edit-sprint');
      await expect(page.getByTestId(ids.editSprint)).toBeVisible();
      await page.getByTestId(ids.editSprintName).blur();
    },
  },
  {
    name: 'sprint-backlog-close',
    route: `/p/SHIP/sprints/${SPRINT_24}/backlog`,
    shell: true,
    arrange: async (page) => {
      await stubShell(page, { sprints: sprintsAtEnd });
      await stubSprintEnd(page);
    },
    ready: async (page) => {
      await expect(page.getByTestId(headerIds.header)).toBeVisible();
    },
    act: async (page) => {
      await more(page, 'close-sprint');
      await expect(page.getByTestId(componentTestIds.dialog)).toBeVisible();
      await expect(page.getByTestId(ids.endRow)).toHaveCount(2);
    },
  },
]);
