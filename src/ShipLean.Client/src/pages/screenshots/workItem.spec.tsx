import { expect } from '@playwright/experimental-ct-react';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { stubSide } from '../../features/workItem/ct/stubSide';
import { workItemTestIds } from '../../features/workItem/testIds';
import { projectSettings, storiesBoardSettings } from './fixtures/workItemSettings';
import { stubShell, PEOPLE, SHELL_MEMBERS, SHELL_SPRINTS } from './shellStub';
import {
  attachments,
  comments,
  crashBug,
  revisions,
  savedCardStory,
  workItemStates,
} from './fixtures/workItem';
import { settingsTestIds } from '../../features/settings/testIds';
import { importTestIds } from '../../features/import/testIds';
import {
  connection,
  importJob,
  failedRow,
  logLines,
  stubImport,
  stubJob,
  stubMapping,
} from '../../features/import/ct/fixtures';
import { pageShots, type Page } from './shoot';
import type { WorkItemDto } from '../../api/workItems';

async function stubItem(page: Page, item: WorkItemDto) {
  await stubShell(page);
  await stubApi(page, item);
  // The form fixture's states, members and sprints are registered last and win; put the shell's back.
  await page.route('**/api/projects/p1/states', (route) => route.fulfill({ json: workItemStates }));
  await page.route('**/api/projects/p1/members', (route) => route.fulfill({ json: SHELL_MEMBERS }));
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: SHELL_SPRINTS }));
  await page.route('**/api/work-items/*/comments', (route) => route.fulfill({ json: comments }));
  await stubSide(page, PEOPLE.priya, attachments, revisions);
}

const itemReady = async (page: Page) => {
  await expect(page.getByTestId(workItemTestIds.title)).toBeVisible();
  await expect(page.getByTestId(workItemTestIds.links)).toBeVisible();
  await expect(page.getByTestId(workItemTestIds.tasks)).toBeVisible();
  await expect(page.getByTestId(workItemTestIds.commentsCount)).toHaveText('3');
  await expect(page.getByTestId(workItemTestIds.attachment)).toHaveCount(2);
  await expect(page.getByTestId(workItemTestIds.history)).toBeVisible();
};

pageShots([
  {
    name: 'work-item',
    route: '/p/SHIP/items/SHIP-141',
    shell: true,
    arrange: (page) => stubItem(page, savedCardStory),
    ready: itemReady,
  },
  {
    name: 'work-item-bug',
    route: '/p/SHIP/items/SHIP-153',
    shell: true,
    arrange: (page) => stubItem(page, crashBug),
    ready: itemReady,
  },
]);

const done = (n: number, total = n, failed = 0) => ({ done: n, total, failed });
const finishedJob = importJob({
  status: 'doneWithErrors',
  startedLabel: 'just now',
  finishedAt: '2026-10-24T10:12:41Z',
  finishedLabel: 'just now',
  progress: {
    ...importJob().progress,
    percent: 100,
    step: 'finishing',
    message: 'Done',
    counts: {
      sprints: done(11),
      issues: done(1200),
      comments: done(3911, 3912, 1),
      attachments: done(640, 641, 1),
      revisions: done(21405, 21406, 1),
    },
  },
});

const errorRows = [
  failedRow,
  {
    ...failedRow,
    id: 'e2',
    step: 'revisions' as const,
    entity: 'revision',
    sourceKey: 'PLATFORM-1102',
    message: 'Jira timed out (504) reading the changelog',
  },
  {
    ...failedRow,
    id: 'e3',
    step: 'comments' as const,
    entity: 'comment',
    sourceKey: 'PLATFORM-1290',
    message: 'Rate limited by Jira (429)',
  },
];

const importProject = { id: 'p-ship', nextItemNumber: 1 };
const importArrange = async (page: Page) => {
  await stubShell(page, { project: importProject });
  await stubImport(page, connection);
  await stubMapping(page);
  // The jobs lists (this project's, and the instance's) carry the finished PLATFORM run and two older imports.
  const jobs = [
    finishedJob,
    importJob({
      id: 'j2',
      jiraProjectKey: 'MOBILE',
      projectKey: 'MOB',
      status: 'done',
      startedLabel: '2 days ago',
      finishedLabel: '2 days ago',
    }),
    importJob({
      id: 'j3',
      jiraProjectKey: 'PAYMENTS',
      projectKey: 'PAY',
      status: 'done',
      startedLabel: '3 weeks ago',
      finishedLabel: '3 weeks ago',
    }),
  ];
  await page.route('**/api/projects/p-ship/import/jobs', (route) => route.fulfill({ json: jobs }));
  await page.route('**/api/import/jobs', (route) => route.fulfill({ json: jobs }));
};

pageShots([
  {
    name: 'settings',
    route: '/p/SHIP/settings/states',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await page.route('**/api/projects/p1/settings', (route) =>
        route.fulfill({ json: projectSettings }),
      );
      await page.route('**/api/projects/p1/boards', (route) =>
        route.fulfill({ json: [{ id: 'b-stories', level: 'stories', name: 'Stories' }] }),
      );
      await page.route('**/api/boards/b-stories/settings', (route) =>
        route.fulfill({ json: storiesBoardSettings }),
      );
    },
    ready: (page) => expect(page.getByTestId(settingsTestIds.stateRow).first()).toBeVisible(),
    act: async (page) => {
      await page.getByTestId(settingsTestIds.stateTypeBug).click();
      await expect(page.getByTestId(settingsTestIds.stateRow)).toHaveCount(6);
      await expect(page.getByTestId(settingsTestIds.stateColumn).first()).toHaveText('New');
    },
  },
  {
    name: 'import',
    route: '/p/SHIP/import?rerun=job1&jira=PLATFORM',
    shell: true,
    arrange: importArrange,
    ready: (page) => expect(page.getByTestId(importTestIds.mappingStep)).toBeVisible(),
  },
  {
    name: 'import-key',
    route: '/p/SHIP/import?rerun=job1&jira=PLATFORM',
    shell: true,
    arrange: importArrange,
    ready: (page) => expect(page.getByTestId(importTestIds.mappingStep)).toBeVisible(),
    act: async (page) => {
      await page.getByTestId(importTestIds.step3).click();
      await expect(page.getByTestId(importTestIds.targetStep)).toBeVisible();
    },
  },
  {
    name: 'import-run',
    route: '/p/SHIP/import?job=job1',
    shell: true,
    arrange: async (page) => {
      await importArrange(page);
      await stubJob(page, finishedJob, logLines, errorRows);
    },
    ready: (page) => expect(page.getByTestId(importTestIds.runErrorRow).first()).toBeVisible(),
  },
]);
