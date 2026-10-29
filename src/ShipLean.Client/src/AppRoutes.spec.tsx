import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from './AppRoutes';
import { meDto, stubInstance, stubJson } from './features/auth/authFixtures';
import { agentsTestIds } from './features/agents/testIds';
import { pagesTestIds } from './features/pages/testIds';
import { stubProjectApi } from './pages/pagesFixtures';
import type { RouteId } from './routes';
import { appTestIds } from './testIds';

test('AppRoutes_WhenLoginOpened_ShouldRenderTheLoginPage', async ({ mount, page }) => {
  await stubInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await expect(page.getByTestId(appTestIds.loginPage)).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
});

// Updated for the pages: the project route now renders the backlog page where the placeholder was.
test('AppRoutes_WhenProjectRouteOpened_ShouldRenderTheShell', async ({ mount, page }) => {
  await stubProjectApi(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId(appTestIds.shellTopBar)).toBeVisible();
  await expect(page.getByTestId(pagesTestIds.backlogPage)).toBeVisible();
});

test('Routes_WhenAuthorizeOpened_ShouldExist', async ({ mount, page }) => {
  await stubProjectApi(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/authorize?request=r-1' } });
  await expect(page.getByTestId(agentsTestIds.authorizePage)).toBeVisible();
});

test('AppRoutes_WhenRootOpened_ShouldRedirectToLogin', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/me', meDto({ hasProjects: false }));
  await mount(<AppRoutes />, { hooksConfig: { route: '/' } });
  await expect(page.getByTestId(appTestIds.loginPage)).toBeVisible();
});

// An address for every route id, and the root each one renders. Only the scaffold roots are asserted, never what the
// shell around them draws.
const scaffolds: Record<RouteId, { path: string; testId: string }> = {
  login: { path: '/login', testId: appTestIds.loginPage },
  newProject: { path: '/projects/new', testId: pagesTestIds.newProjectPage },
  instanceSettings: { path: '/instance/users', testId: pagesTestIds.instanceSettingsPage },
  profile: { path: '/profile', testId: pagesTestIds.profilePage },
  importStart: { path: '/import', testId: pagesTestIds.importPage },
  authorize: { path: '/authorize?request=r-1', testId: agentsTestIds.authorizePage },
  storiesBacklog: { path: '/p/SHIP/backlogs/stories', testId: pagesTestIds.backlogPage },
  featuresBacklog: { path: '/p/SHIP/backlogs/features', testId: pagesTestIds.backlogPage },
  epicsBacklog: { path: '/p/SHIP/backlogs/epics', testId: pagesTestIds.backlogPage },
  workItem: { path: '/p/SHIP/items/SHIP-141', testId: pagesTestIds.workItemPage },
  workItemAttachment: {
    path: '/p/SHIP/items/SHIP-101/attachments/a1',
    testId: pagesTestIds.attachmentPage,
  },
  storiesBoard: { path: '/p/SHIP/boards/stories', testId: pagesTestIds.boardPage },
  featuresBoard: { path: '/p/SHIP/boards/features', testId: pagesTestIds.boardPage },
  epicsBoard: { path: '/p/SHIP/boards/epics', testId: pagesTestIds.boardPage },
  sprints: { path: '/p/SHIP/sprints', testId: pagesTestIds.sprintsPage },
  sprintBacklog: {
    path: '/p/SHIP/sprints/sprint-24/backlog',
    testId: pagesTestIds.sprintBacklogPage,
  },
  taskboard: { path: '/p/SHIP/sprints/sprint-24/taskboard', testId: pagesTestIds.taskboardPage },
  capacity: { path: '/p/SHIP/sprints/sprint-24/capacity', testId: pagesTestIds.capacityPage },
  jiraImport: { path: '/p/SHIP/import', testId: pagesTestIds.importPage },
  projectSettings: {
    path: '/p/SHIP/settings/states',
    testId: pagesTestIds.projectSettingsPage,
  },
};

for (const [id, scaffold] of Object.entries(scaffolds)) {
  test(`Routes_WhenEachOpened_ShouldRenderScaffoldRoot_${id}`, async ({ mount, page }) => {
    await stubProjectApi(page);
    await mount(<AppRoutes />, { hooksConfig: { route: scaffold.path } });
    await expect(page.getByTestId(scaffold.testId)).toBeVisible();
  });
}

test('Backlog_WhenUnknownKey_ShouldShowNotFound', async ({ mount, page }) => {
  await stubProjectApi(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/PART/backlogs/stories' } });
  await expect(page.getByTestId(pagesTestIds.notFound)).toBeVisible();
  await expect(page.getByTestId(pagesTestIds.backlogPage)).toHaveCount(0);
});

test('Routes_WhenUnknownProjectRoute_ShouldShowNotFound', async ({ mount, page }) => {
  await stubProjectApi(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/nowhere' } });
  await expect(page.getByTestId(pagesTestIds.notFound)).toBeVisible();
});

test('Sprint_WhenUnknownId_ShouldShowNotFound', async ({ mount, page }) => {
  await stubProjectApi(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/sprints/nope/backlog' } });
  await expect(page.getByTestId(pagesTestIds.notFound)).toBeVisible();
});

// The section the slot gets is pinned in pages/pageWiring.test.tsx; here the page must simply mount with the query.
test('Taskboard_WhenSettingsInQuery_ShouldPassSectionToSlot', async ({ mount, page }) => {
  await stubProjectApi(page);
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/p/SHIP/sprints/sprint-24/taskboard?settings=fields' },
  });
  await expect(page.getByTestId(pagesTestIds.slotTaskboard)).toBeVisible();
  await expect(page.getByTestId(pagesTestIds.slotTaskboardSettings)).toBeVisible();
});

test('Backlog_WhenModeInQuery_ShouldMountThatModesSlot', async ({ mount, page }) => {
  await stubProjectApi(page);
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  await expect(page.getByTestId(pagesTestIds.slotPlanningMode)).toBeVisible();
  await expect(page.getByTestId(pagesTestIds.slotListMode)).toHaveCount(0);
});

test('Routes_WhenUnknownInstallPath_ShouldShowNotFound', async ({ mount, page }) => {
  await stubProjectApi(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/nowhere' } });
  await expect(page.getByTestId(pagesTestIds.notFound)).toBeVisible();
});

test('Backlog_WhenSprintsFailToLoad_ShouldStillRenderItsSlots', async ({ mount, page }) => {
  await stubProjectApi(page);
  await page.route('**/api/projects/*/sprints', (route) =>
    route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: '{"error":"x","code":"internal_error"}',
    }),
  );
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId(pagesTestIds.slotListMode)).toBeVisible();
  await expect(page.getByTestId(pagesTestIds.notFound)).toHaveCount(0);
  await expect(page.getByTestId(pagesTestIds.loadFailed)).toHaveCount(0);
});

// A failed refetch after a good load must not take the page down: the slots hold drafts and open dialogs.
for (const failing of ['by-key', 'sprints'] as const) {
  test(`Capacity_WhenRefetchOf_${failing}_Fails_ShouldKeepSlotsMounted`, async ({
    mount,
    page,
  }) => {
    await stubProjectApi(page);
    await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/sprints/sprint-24/capacity' } });
    await expect(page.getByTestId(pagesTestIds.slotCapacity)).toBeVisible();

    await page.route(
      failing === 'by-key' ? '**/api/projects/by-key/*' : '**/api/projects/*/sprints',
      (route) =>
        route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: '{"error":"x","code":"internal_error"}',
        }),
    );
    const refetch = page.waitForResponse((response) => response.status() === 500);
    await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));
    await refetch;

    await expect(page.getByTestId(pagesTestIds.slotCapacity)).toBeVisible();
    await expect(page.getByTestId(pagesTestIds.loadFailed)).toHaveCount(0);
  });
}

test('InstanceSettings_WhenOpened_ShouldLinkEveryTabAndMarkTheCurrentOne', async ({
  mount,
  page,
}) => {
  await stubProjectApi(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/instance/users' } });
  const tabs = page.getByTestId(pagesTestIds.instanceSettingsTab);
  await expect(tabs).toHaveText(['General', 'Users', 'Projects', 'Integrations', 'Agents']);
  await expect(tabs.nth(4)).toHaveAttribute('href', '/instance/agents');
  await expect(tabs.nth(1)).toHaveAttribute('aria-current', 'page');
});
