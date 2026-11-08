import { expect } from '@playwright/experimental-ct-react';
import type { ProjectSummaryDto } from '../../api/projects';
import { backlogView, ROWS } from '../../features/backlog/ct/fixtures';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { backlogTestIds } from '../../features/backlog/testIds';
import { realtimeTestIds } from '../../features/realtime/testIds';
import { shellTestIds } from '../../features/shell/testIds';
import { sprintsTestIds } from '../../features/sprints/testIds';
import { touchTestIds } from '../../touch/testIds';
import { stubSprintsList, sprintsWithGoals } from './fixtures/sprints';
import { SHELL_PROJECT, SHELL_SPRINTS, stubShell } from './shellStub';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';

// The touch drawer on a phone (SHIP-713): the side menu with every section's sub-pages, the same on Sprints, the project
// picker as a bottom sheet over the drawer and the user menu opening upwards. The shots use shell:false and wait for the
// Live dot themselves: a mask would paint over the scrim that covers the dot. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/shellDrawer.phone.spec.tsx.

const BACKLOG = '/p/SHIP/backlogs/stories';

const summary = (id: string, key: string, name: string, memberCount: number) => ({
  id,
  key,
  name,
  role: 'member' as const,
  memberCount,
});

const PROJECTS: ProjectSummaryDto[] = [
  { ...summary(SHELL_PROJECT.id, 'SHIP', 'Storefront', 5), role: 'admin' },
  summary('p2', 'PAY', 'Payments', 7),
  summary('p3', 'MOB', 'Mobile app', 4),
  summary('p4', 'DATA', 'Data platform', 3),
  summary('p5', 'OPS', 'Operations', 6),
];

async function arrangeBacklog(page: Page) {
  await stubShell(page, { projects: PROJECTS });
  // The backlog stub answers sprints with none; the drawer's current sprint needs the shell's.
  await stubBacklog(page, backlogView(ROWS), { sprints: SHELL_SPRINTS });
}

async function backlogReady(page: Page) {
  await expect(page.getByTestId(backlogTestIds.row).first()).toBeVisible();
  await expect(page.getByTestId(realtimeTestIds.liveDot)).toHaveAttribute('data-state', 'offline');
}

async function openDrawer(page: Page) {
  await page.getByTestId(touchTestIds.topBarMenu).tap();
  const drawer = page.getByTestId(touchTestIds.drawer);
  await expect(drawer.getByTestId(shellTestIds.userButton)).toContainText('Priya Shah');
  await expect(drawer.getByTestId(shellTestIds.navCurrentSprint)).toContainText('Sprint 24');
  return drawer;
}

phonePageShots([
  {
    name: 'drawer',
    route: BACKLOG,
    shell: false,
    arrange: arrangeBacklog,
    ready: backlogReady,
    act: async (page) => {
      await openDrawer(page);
    },
  },
  {
    name: 'drawer-sprints',
    route: '/p/SHIP/sprints',
    shell: false,
    arrange: async (page) => {
      await stubShell(page, { sprints: sprintsWithGoals, projects: PROJECTS });
      await stubSprintsList(page);
    },
    ready: async (page) => {
      await expect(page.getByTestId(sprintsTestIds.listRow).first()).toBeVisible();
      await expect(page.getByTestId(realtimeTestIds.liveDot)).toHaveAttribute(
        'data-state',
        'offline',
      );
    },
    act: async (page) => {
      const drawer = await openDrawer(page);
      await expect(drawer.getByTestId(shellTestIds.navAllSprints)).toHaveAttribute(
        'aria-current',
        'page',
      );
    },
  },
  {
    name: 'picker',
    route: BACKLOG,
    shell: false,
    arrange: arrangeBacklog,
    ready: backlogReady,
    act: async (page) => {
      const drawer = await openDrawer(page);
      await drawer.getByTestId(shellTestIds.projectButton).tap();
      const sheet = page.getByTestId(touchTestIds.sheet);
      await expect(sheet.getByTestId(shellTestIds.pickerOption)).toHaveCount(PROJECTS.length);
    },
  },
  {
    name: 'user-menu',
    route: BACKLOG,
    shell: false,
    arrange: arrangeBacklog,
    ready: backlogReady,
    act: async (page) => {
      const drawer = await openDrawer(page);
      await drawer.getByTestId(shellTestIds.userButton).tap();
      await expect(drawer.getByTestId(shellTestIds.userMenuSignOut)).toBeVisible();
    },
  },
]);
