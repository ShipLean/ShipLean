import { expect, test } from '@playwright/experimental-ct-react';
import type { MeDto } from '../../../../api/me';
import { ActionsHarness } from './ct/ActionsHarness';
import { actionsHarnessIds as ids } from './ct/harnessIds';

const me: MeDto = {
  id: 'me',
  name: 'Priya Shah',
  email: 'priya@example.test',
  initials: 'PS',
  avatarColour: '#2f7fd8',
  photoUrl: null,
  isInstanceAdmin: false,
  status: 'active',
  hasProjects: true,
  canCreateProjects: true,
  theme: 'dark',
  density: 'compact',
};

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; url(): string; postDataJSON(): unknown };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const stub = async (page: Routable, refuse = false) => {
  const puts: { path: string; body: unknown }[] = [];
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === 'GET') {
      return route.fulfill({ json: path === '/api/me' ? me : [] });
    }
    puts.push({ path, body: request.postDataJSON() });
    if (refuse) return route.fulfill({ status: 400, json: { code: 'validation', error: 'No.' } });
    return route.fulfill({ status: 204 });
  });
  return puts;
};

const mountOn = (mount: (c: React.JSX.Element, o: object) => Promise<unknown>) =>
  mount(<ActionsHarness />, { hooksConfig: { route: '/p/SHIP/sprints' } });

test('Actions_WhenCreateBugPicked_ShouldOpenTheStoriesBacklogWithNewBug', async ({
  mount,
  page,
}) => {
  await stub(page);
  const component = (await mountOn(mount)) as Awaited<ReturnType<typeof mount>>;
  await component.getByTestId(ids.pick('create-bug')).click();
  await expect(component.getByTestId(ids.location)).toHaveText('/p/SHIP/backlogs/stories?new=bug');
});

test('Actions_WhenToggleThemePicked_ShouldSaveAndApplyTheOtherTheme', async ({ mount, page }) => {
  const puts = await stub(page);
  const component = (await mountOn(mount)) as Awaited<ReturnType<typeof mount>>;
  await component.getByTestId(ids.pick('toggle-theme')).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0]).toEqual({ path: '/api/me/preferences/theme', body: 'light' });
});

test('Actions_WhenTheThemeSaveIsRefused_ShouldRevertAndToast', async ({ mount, page }) => {
  await stub(page, true);
  const component = (await mountOn(mount)) as Awaited<ReturnType<typeof mount>>;
  await component.getByTestId(ids.pick('toggle-theme')).click();
  await expect(component.getByTestId(ids.toasts)).toHaveText('That choice could not be saved.');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
});
