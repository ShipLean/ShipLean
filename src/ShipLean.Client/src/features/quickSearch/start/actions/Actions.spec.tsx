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

interface Holds {
  refuse?: boolean;
  /** Held until released, then the refused PUT is answered. */
  putGate?: Promise<void>;
  /** Held until released, then /api/me is answered. */
  meGate?: Promise<void>;
}

const stub = async (page: Routable, { refuse = false, putGate, meGate }: Holds = {}) => {
  const puts: { path: string; body: unknown }[] = [];
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === 'GET') {
      if (path === '/api/me') await meGate;
      return route.fulfill({ json: path === '/api/me' ? me : [] });
    }
    puts.push({ path, body: request.postDataJSON() });
    await putGate;
    if (refuse) return route.fulfill({ status: 400, json: { code: 'validation', error: 'No.' } });
    return route.fulfill({ status: 204 });
  });
  return puts;
};

const route = { hooksConfig: { route: '/p/SHIP/sprints' } };

test('Actions_WhenCreateBugPicked_ShouldOpenTheStoriesBacklogWithNewBug', async ({
  mount,
  page,
}) => {
  await stub(page);
  const component = await mount(<ActionsHarness />, route);
  await component.getByTestId(ids.pick('create-bug')).click();
  await expect(component.getByTestId(ids.location)).toHaveText('/p/SHIP/backlogs/stories?new=bug');
});

test('Actions_WhenToggleThemePicked_ShouldSaveAndApplyTheOtherTheme', async ({ mount, page }) => {
  const puts = await stub(page);
  const component = await mount(<ActionsHarness />, route);
  await expect(component.getByTestId(ids.detail('toggle-theme'))).toHaveText('Dark → Light');
  await component.getByTestId(ids.pick('toggle-theme')).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0]).toEqual({ path: '/api/me/preferences/theme', body: 'light' });
});

test('Actions_WhenTheThemeSaveIsRefused_ShouldRevertAndToast', async ({ mount, page }) => {
  let release: () => void = () => undefined;
  const putGate = new Promise<void>((resolve) => (release = resolve));
  await stub(page, { refuse: true, putGate });
  const component = await mount(<ActionsHarness />, route);
  await expect(component.getByTestId(ids.detail('toggle-theme'))).toHaveText('Dark → Light');
  await component.getByTestId(ids.pick('toggle-theme')).click();
  // The rows have unmounted, as the dialog does on done, while the save is still held.
  await expect(component.getByTestId(ids.pick('toggle-theme'))).toHaveCount(0);
  release();
  await expect(component.getByTestId(ids.toasts)).toHaveText('That choice could not be saved.');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
});

test('Actions_WhenTheUserIsStillLoading_ShouldNotOfferToggleTheme', async ({ mount, page }) => {
  let release: () => void = () => undefined;
  const meGate = new Promise<void>((resolve) => (release = resolve));
  await stub(page, { meGate });
  const component = await mount(<ActionsHarness />, route);
  await expect(component.getByTestId(ids.pick('create-bug'))).toBeVisible();
  await expect(component.getByTestId(ids.pick('toggle-theme'))).toHaveCount(0);
  release();
  await expect(component.getByTestId(ids.pick('toggle-theme'))).toBeVisible();
});
