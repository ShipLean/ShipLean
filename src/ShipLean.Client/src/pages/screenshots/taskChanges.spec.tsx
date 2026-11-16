import { expect } from '@playwright/experimental-ct-react';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { TaskChangesDto, TaskCommitsSectionDto } from '@shiplean/core/api/taskChanges';
import { stubChanges } from '../../features/taskChanges/ct/stubChanges';
import { TASK } from '../../features/taskChanges/ct/fixtures';
import { taskChangesTestIds as ids } from '../../features/taskChanges/testIds';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { stubSide } from '../../features/workItem/ct/stubSide';
import { workItemTestIds } from '../../features/workItem/testIds';
import {
  CHANGES,
  CHANGES_NO_ACCESS,
  CHANGES_OUTDATED,
  SECTION,
  SECTION_EMPTY,
} from './fixtures/taskChangesFixtures';
import { attachments, comments, crashBug, revisions, workItemStates } from './fixtures/workItem';
import { pageShots, type Page } from './shoot';
import { PEOPLE, SHELL_MEMBERS, SHELL_SPRINTS, stubShell } from './shellStub';

// The Changes page and the Task page's Changes section, named after design/screens/task-changes*.png. The Bug page's
// section is shot by work-item-bug in workItem.spec.tsx.

const changesRoute = `/p/SHIP/items/${TASK.key}/changes`;

async function stubReview(page: Page, changes: TaskChangesDto, section = SECTION) {
  await stubShell(page);
  await stubChanges(page, TASK, changes, section);
}

const reviewReady = async (page: Page) => {
  await expect(page.getByTestId(ids.toolbar)).toBeVisible();
  await expect(page.getByTestId(ids.diffFile)).not.toHaveCount(0);
  await expect(page.getByTestId(ids.comments)).toBeVisible();
};

async function stubTask(page: Page, section: TaskCommitsSectionDto) {
  const item: WorkItemDto = { ...crashBug, type: { id: 'task', name: 'Task' } };
  await stubShell(page);
  await stubApi(page, item);
  // The form fixture's states, members and sprints are registered last and win; put the shell's back.
  await page.route('**/api/projects/p1/states', (route) => route.fulfill({ json: workItemStates }));
  await page.route('**/api/projects/p1/members', (route) => route.fulfill({ json: SHELL_MEMBERS }));
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: SHELL_SPRINTS }));
  await page.route('**/api/work-items/*/comments', (route) => route.fulfill({ json: comments }));
  await page.route('**/api/work-items/*/commits', (route) => route.fulfill({ json: section }));
  await stubSide(page, PEOPLE.priya, attachments, revisions);
}

const taskRoute = `/p/SHIP/items/${crashBug.key}`;

pageShots([
  {
    name: 'task-changes',
    route: changesRoute,
    shell: true,
    arrange: (page) => stubReview(page, CHANGES),
    ready: reviewReady,
  },
  {
    name: 'task-changes-split',
    route: `${changesRoute}?layout=split`,
    shell: true,
    arrange: (page) => stubReview(page, CHANGES),
    ready: reviewReady,
  },
  {
    name: 'task-changes-compose',
    route: changesRoute,
    shell: true,
    arrange: (page) => stubReview(page, CHANGES),
    ready: reviewReady,
    act: async (page) => {
      await page.locator('[data-gutter] [data-line-type="change-addition"]').first().hover();
      await page.locator('[data-utility-button]').click();
      await expect(page.getByTestId(ids.composer)).toBeVisible();
    },
  },
  {
    name: 'task-changes-commits',
    route: changesRoute,
    shell: true,
    arrange: (page) => stubReview(page, CHANGES),
    ready: reviewReady,
    act: async (page) => {
      await page.getByTestId(ids.commitPicker).click();
      await expect(page.getByTestId(ids.commitOption).first()).toBeVisible();
    },
  },
  {
    name: 'task-changes-request',
    route: changesRoute,
    shell: true,
    arrange: (page) => stubReview(page, CHANGES),
    ready: reviewReady,
    act: async (page) => {
      await page.getByTestId(ids.requestChanges).click();
      await expect(page.getByTestId(ids.requestDialog)).toBeVisible();
    },
  },
  {
    name: 'task-changes-approve',
    route: changesRoute,
    shell: true,
    arrange: (page) => stubReview(page, CHANGES),
    ready: reviewReady,
    act: async (page) => {
      await page.getByTestId(ids.approve).click();
      await expect(page.getByTestId(ids.approveDialog)).toBeVisible();
    },
  },
  {
    name: 'task-changes-outdated',
    route: changesRoute,
    shell: true,
    arrange: (page) => stubReview(page, CHANGES_OUTDATED),
    ready: async (page) => {
      await reviewReady(page);
      await expect(page.getByTestId(ids.commentOldLine)).toBeVisible();
    },
  },
  {
    name: 'task-changes-noaccess',
    route: changesRoute,
    shell: true,
    arrange: (page) => stubReview(page, CHANGES_NO_ACCESS),
    ready: async (page) => {
      await expect(page.getByTestId(ids.toolbar)).toBeVisible();
      await expect(page.getByTestId(ids.diffNoAccess)).toBeVisible();
    },
  },
  {
    name: 'task-changes-task',
    route: taskRoute,
    shell: true,
    arrange: (page) => stubTask(page, SECTION),
    ready: async (page) => {
      await expect(page.getByTestId(workItemTestIds.title)).toBeVisible();
      await expect(page.getByTestId(ids.section)).toBeVisible();
      await expect(page.getByTestId(ids.sectionRow)).not.toHaveCount(0);
    },
  },
  {
    name: 'task-changes-empty',
    route: taskRoute,
    shell: true,
    arrange: (page) => stubTask(page, SECTION_EMPTY),
    ready: async (page) => {
      await expect(page.getByTestId(workItemTestIds.title)).toBeVisible();
      await expect(page.getByTestId(ids.sectionEmpty)).toBeVisible();
    },
  },
]);
