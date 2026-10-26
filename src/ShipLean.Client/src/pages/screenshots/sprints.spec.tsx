import { expect } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { sprintsTestIds as ids } from '../../features/sprints/testIds';
import { pageShots } from './shoot';
import { stubShell } from './shellStub';
import {
  SPRINT_24,
  sprintsAtEnd,
  sprintsWithGoals,
  stubCapacity,
  stubSprintBacklog,
  stubSprintEnd,
  stubSprintsList,
} from './fixtures/sprints';

pageShots([
  {
    name: 'sprints',
    route: '/p/SHIP/sprints',
    shell: true,
    arrange: async (page) => {
      await stubShell(page, { sprints: sprintsWithGoals });
      await stubSprintsList(page);
    },
    ready: async (page) => {
      await expect(page.getByTestId(ids.listRow).first()).toBeVisible();
      await expect(page.getByTestId(ids.velocityBar).first()).toBeVisible();
      await expect(page.getByTestId(ids.rowCommitted).first()).not.toHaveText('');
    },
  },
  {
    name: 'sprint-backlog',
    route: `/p/SHIP/sprints/${SPRINT_24}/backlog`,
    shell: true,
    arrange: async (page) => {
      await stubShell(page, { sprints: sprintsWithGoals });
      await stubSprintBacklog(page);
    },
    ready: async (page) => {
      await expect(page.getByTestId(ids.storyRow).first()).toBeVisible();
      await expect(page.getByTestId(ids.burndownPoint).first()).toBeVisible();
      await expect(page.getByTestId(ids.railPerson).first()).toBeVisible();
    },
  },
  {
    name: 'capacity',
    route: `/p/SHIP/sprints/${SPRINT_24}/capacity`,
    shell: true,
    arrange: async (page) => {
      await stubShell(page, { sprints: sprintsWithGoals });
      await stubCapacity(page);
    },
    ready: async (page) => {
      await expect(page.getByTestId(ids.capacityRow)).toHaveCount(5);
      await expect(page.getByTestId(ids.capacityCallout)).toBeVisible();
    },
  },
  {
    name: 'sprint-end',
    route: '/p/SHIP/sprints',
    shell: true,
    arrange: async (page) => {
      await stubShell(page, { sprints: sprintsAtEnd });
      await stubSprintEnd(page);
    },
    ready: async (page) => {
      await expect(page.getByTestId(componentTestIds.dialog)).toBeVisible();
      await expect(page.getByTestId(ids.endRow)).toHaveCount(2);
    },
  },
]);
