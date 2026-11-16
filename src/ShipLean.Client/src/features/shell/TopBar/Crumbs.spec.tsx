import { expect, test } from '@playwright/experimental-ct-react';
import { SHELL_PROJECT, SHELL_SPRINTS, stubShell } from '../../../pages/screenshots/shellStub';
import { shellTestIds as ids } from '../testIds';
import { Crumbs } from './Crumbs';

// The sprint crumb (K2, SHIP-1626 D1): every sprint tab names its sprint, on desktop as on a phone, and says "Sprint"
// until the sprints load.

const TABS = [
  ['backlog', 'Backlog'],
  ['taskboard', 'Taskboard'],
  ['capacity', 'Capacity'],
  ['retro', 'Retro'],
  ['demo', 'Demo'],
] as const;

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
type Route = Parameters<Exclude<Parameters<Page['route']>[1], undefined>>[0];

const sprint = SHELL_SPRINTS.find((s) => s.status === 'current')!;

for (const [segment, title] of TABS) {
  test(`Crumbs_WhenOnASprintTab_ShouldShowTheSprintName_${segment}`, async ({ mount, page }) => {
    await stubShell(page);
    await mount(<Crumbs />, { hooksConfig: { route: `/p/SHIP/sprints/${sprint.id}/${segment}` } });

    await expect(page.getByTestId(ids.crumb)).toHaveText(['SHIP', 'Sprints', sprint.name, title]);
  });
}

test('Crumbs_WhenSprintStillLoading_ShouldShowSprint', async ({ mount, page }) => {
  await stubShell(page);
  let release: (route: Route) => void = () => undefined;
  const held = new Promise<Route>((resolve) => (release = resolve));
  await page.route(`**/api/projects/${SHELL_PROJECT.id}/sprints`, (route) => release(route));
  await mount(<Crumbs />, { hooksConfig: { route: `/p/SHIP/sprints/${sprint.id}/backlog` } });
  const route = await held;

  await expect(page.getByTestId(ids.crumb)).toHaveText(['SHIP', 'Sprints', 'Sprint', 'Backlog']);

  await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(SHELL_SPRINTS) });
  await expect(page.getByTestId(ids.crumb)).toHaveText(['SHIP', 'Sprints', sprint.name, 'Backlog']);
});
