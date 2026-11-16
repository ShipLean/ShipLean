import { expect } from '@playwright/experimental-ct-react';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { stubSide } from '../../features/workItem/ct/stubSide';
import { workItemTestIds } from '../../features/workItem/testIds';
import { retroActionDetail, stubRetros } from '../../features/retros/ct/retroFixtures';
import { retrosTestIds } from '../../features/retros/testIds';
import {
  actionItem,
  attachments,
  comments,
  crashBug,
  revisions,
  savedCardStory,
  workItemStates,
} from './fixtures/workItem';
import { PEOPLE, SHELL_MEMBERS, SHELL_SPRINTS, stubShell } from './shellStub';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';
import type { WorkItemDto } from '@shiplean/core/api/workItems';

// The whole work item page on a phone (SHIP-714, SHIP-1626): a story, a bug and an action item inside the shell, with the
// top bar's ‹ back, the header, the wrapped section chips, the field tiles and the sections below. The phone twin of
// workItem.spec.tsx. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/workItem.phone.spec.tsx.

async function stubItem(page: Page, item: WorkItemDto, itemAttachments = attachments) {
  await stubShell(page);
  await stubApi(page, item);
  // The form fixture's states, members and sprints are registered last and win; put the shell's back.
  await page.route('**/api/projects/p1/states', (route) => route.fulfill({ json: workItemStates }));
  await page.route('**/api/projects/p1/members', (route) => route.fulfill({ json: SHELL_MEMBERS }));
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill({ json: SHELL_SPRINTS }));
  await page.route('**/api/work-items/*/comments', (route) => route.fulfill({ json: comments }));
  await stubSide(page, PEOPLE.priya, itemAttachments, revisions);
}

const itemReady = async (page: Page) => {
  await expect(page.getByTestId(workItemTestIds.title)).toBeVisible();
  await expect(page.getByTestId(workItemTestIds.links)).toBeVisible();
  await expect(page.getByTestId(workItemTestIds.tasks)).toBeVisible();
  await expect(page.getByTestId(workItemTestIds.commentsCount)).toHaveText('3');
  await expect(page.getByTestId(workItemTestIds.attachment)).toHaveCount(2);
  await expect(page.getByTestId(workItemTestIds.history)).toBeVisible();
};

phonePageShots([
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
  {
    name: 'work-item-action',
    route: '/p/SHIP/items/SHIP-188',
    shell: true,
    arrange: async (page) => {
      await stubItem(page, actionItem, []);
      await stubRetros(page);
    },
    ready: async (page) => {
      await expect(page.getByTestId(workItemTestIds.title)).toBeVisible();
      await expect(page.getByTestId(retrosTestIds.retroActionWhy)).toBeVisible();
      await expect(page.getByTestId(retrosTestIds.retroActionWhyPoint)).toHaveCount(
        retroActionDetail.points.length,
      );
    },
    // Why this exists sits below the fold; bring it up so the shot shows the section.
    act: async (page) => {
      await page.getByTestId(retrosTestIds.retroActionWhy).evaluate((section) => {
        section.scrollIntoView({ block: 'start' });
        // Clear of the pinned top bar.
        window.scrollBy(0, -56);
      });
      await expect(page.getByTestId(retrosTestIds.retroActionWhyPoint).first()).toBeInViewport();
    },
  },
]);
