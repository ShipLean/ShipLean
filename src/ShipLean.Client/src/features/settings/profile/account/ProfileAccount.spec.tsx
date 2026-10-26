import { expect, test } from '@playwright/experimental-ct-react';
import type { MeDto } from '../../../../api/me';
import type { ProjectSummaryDto } from '../../../../api/projects';
import { settingsTestIds as ids } from '../../testIds';
import { ProfileAccount } from './ProfileAccount';

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
  theme: 'os',
  density: 'compact',
};

const projectOf = (key: string, role: 'admin' | 'member'): ProjectSummaryDto => ({
  id: `id-${key}`,
  key,
  name: `${key} project`,
  role,
  memberCount: 3,
});

interface Calls {
  list: string[];
  bodies: unknown[];
}

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; url(): string; postDataJSON(): unknown };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const stub = async (
  page: Routable,
  options: { refuse?: { path: string; message: string }; projects?: ProjectSummaryDto[] } = {},
): Promise<Calls> => {
  const calls: Calls = { list: [], bodies: [] };
  let projects = options.projects ?? [projectOf('SHIP', 'admin'), projectOf('PAY', 'member')];
  let current = me;
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const method = request.method();
    const path = new URL(request.url()).pathname;
    if (method === 'GET') {
      return route.fulfill({ json: path === '/api/me' ? current : projects });
    }
    calls.list.push(`${method} ${path}`);
    calls.bodies.push(request.postDataJSON());
    if (options.refuse?.path === path) {
      return route.fulfill({
        status: 400,
        json: { code: 'validation', error: options.refuse.message },
      });
    }
    if (path.endsWith('/leave')) {
      projects = projects.filter((p) => !path.includes(p.id));
    }
    if (path === '/api/auth/password') return route.fulfill({ json: current });
    if (path.startsWith('/api/me/preferences/') && !path.endsWith('reset-views')) {
      const key = path.split('/').pop() as 'theme' | 'density';
      current = { ...current, [key]: request.postDataJSON() as string };
    }
    return route.fulfill({ status: 204 });
  });
  return calls;
};

test('Password_WhenChecksUnmet_ShouldKeepChangeDisabled', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfileAccount />);
  const submit = view.getByTestId(ids.passwordSubmit);

  await expect(submit).toBeDisabled();
  await view.getByTestId(ids.passwordCurrent).fill('old-password-1');
  await view.getByTestId(ids.passwordNew).fill('short');
  await view.getByTestId(ids.passwordConfirm).fill('short');
  await expect(submit).toBeDisabled();
  await view.getByTestId(ids.passwordNew).fill('old-password-1');
  await view.getByTestId(ids.passwordConfirm).fill('old-password-1');
  await expect(submit).toBeDisabled();
  await view.getByTestId(ids.passwordNew).fill('a-new-password');
  await expect(submit).toBeDisabled();
  await view.getByTestId(ids.passwordConfirm).fill('a-new-password');
  await expect(submit).toBeEnabled();
});

test('Password_WhenChanged_ShouldPostAndClearTheForm', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<ProfileAccount />);

  await view.getByTestId(ids.passwordCurrent).fill('old-password-1');
  await view.getByTestId(ids.passwordNew).fill('a-new-password');
  await view.getByTestId(ids.passwordConfirm).fill('a-new-password');
  await view.getByTestId(ids.passwordSubmit).click();

  await expect(view.getByTestId(ids.passwordCurrent)).toHaveValue('');
  await expect(view.getByTestId(ids.passwordNew)).toHaveValue('');
  await expect(view.getByTestId(ids.passwordConfirm)).toHaveValue('');
  expect(calls.list).toEqual(['POST /api/auth/password']);
  expect(calls.bodies).toEqual([{ current: 'old-password-1', new: 'a-new-password' }]);
});

test('Password_WhenServerRefuses_ShouldShowItsMessageAndKeepTheForm', async ({ mount, page }) => {
  await stub(page, {
    refuse: { path: '/api/auth/password', message: 'The current password is wrong.' },
  });
  const view = await mount(<ProfileAccount />);

  await view.getByTestId(ids.passwordCurrent).fill('wrong-password');
  await view.getByTestId(ids.passwordNew).fill('a-new-password');
  await view.getByTestId(ids.passwordConfirm).fill('a-new-password');
  await view.getByTestId(ids.passwordSubmit).click();

  await expect(view.getByTestId(ids.passwordError)).toHaveText('The current password is wrong.');
  await expect(view.getByTestId(ids.passwordNew)).toHaveValue('a-new-password');
  await expect(view.getByTestId(ids.passwordCurrent)).toHaveValue('');
});

test('Appearance_WhenThemeAndDensityPicked_ShouldApplyAtOnceAndSave', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<ProfileAccount />);

  await view.locator(`[data-testid="${ids.themeOption}"][data-value="dark"]`).click();
  await view.locator(`[data-testid="${ids.densityOption}"][data-value="comfortable"]`).click();

  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(page.locator('html')).toHaveAttribute('data-density', 'comfortable');
  await expect(
    view.locator(`[data-testid="${ids.themeOption}"][data-value="dark"]`),
  ).toHaveAttribute('aria-pressed', 'true');
  expect(calls.list).toEqual(['PUT /api/me/preferences/theme', 'PUT /api/me/preferences/density']);
  expect(calls.bodies).toEqual(['dark', 'comfortable']);
});

test('Appearance_WhenServerRefuses_ShouldPutTheOldChoiceBack', async ({ mount, page }) => {
  await stub(page, { refuse: { path: '/api/me/preferences/theme', message: 'Not saved.' } });
  const view = await mount(<ProfileAccount />);

  await view.locator(`[data-testid="${ids.themeOption}"][data-value="dark"]`).click();

  await expect(view.getByTestId(ids.appearanceError)).toHaveText('Not saved.');
  await expect(page.locator('html')).not.toHaveAttribute('data-theme', 'dark');
  await expect(view.locator(`[data-testid="${ids.themeOption}"][data-value="os"]`)).toHaveAttribute(
    'aria-pressed',
    'true',
  );
});

test('Projects_WhenLeaveConfirmed_ShouldPostAndDropTheRow', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<ProfileAccount />);
  await expect(view.getByTestId(ids.myProjectRow)).toHaveCount(2);

  await view.locator(`[data-key="PAY"] [data-testid="${ids.myProjectLeave}"]`).click();
  await view.getByTestId(ids.myProjectLeaveConfirm).click();

  await expect(view.getByTestId(ids.myProjectRow)).toHaveCount(1);
  expect(calls.list).toEqual(['POST /api/projects/id-PAY/leave']);
});

test('Projects_WhenLeaveCancelled_ShouldPostNothing', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<ProfileAccount />);

  await view.locator(`[data-key="PAY"] [data-testid="${ids.myProjectLeave}"]`).click();
  await view.getByTestId(ids.myProjectLeaveCancel).click();

  await expect(view.getByTestId(ids.myProjectLeaveConfirm)).toHaveCount(0);
  expect(calls.list).toEqual([]);
});

test('Projects_WhenOnlyAdminLeaves_ShouldShowServerMessageAndKeepTheRow', async ({
  mount,
  page,
}) => {
  const message = 'You are the only admin. Make someone else an admin before you leave.';
  await stub(page, { refuse: { path: '/api/projects/id-SHIP/leave', message } });
  const view = await mount(<ProfileAccount />);

  await view.locator(`[data-key="SHIP"] [data-testid="${ids.myProjectLeave}"]`).click();
  await view.getByTestId(ids.myProjectLeaveConfirm).click();

  await expect(view.getByTestId(ids.myProjectLeaveError)).toHaveText(message);
  await expect(view.getByTestId(ids.myProjectRow)).toHaveCount(2);
});

test('Sessions_WhenActionsClicked_ShouldCallTheirEndpoints', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<ProfileAccount />);

  await view.getByTestId(ids.signOutOthers).click();
  await expect.poll(() => calls.list).toContain('POST /api/auth/sign-out-others');
  await view.getByTestId(ids.resetViews).click();
  await expect.poll(() => calls.list).toContain('POST /api/me/preferences/reset-views');
});

test('Sessions_WhenResetRefused_ShouldShowTheMessage', async ({ mount, page }) => {
  await stub(page, { refuse: { path: '/api/me/preferences/reset-views', message: 'Nope.' } });
  const view = await mount(<ProfileAccount />);

  await view.getByTestId(ids.resetViews).click();

  await expect(view.getByTestId(ids.sessionsError)).toHaveText('Nope.');
});

test('Password_WhenEnterPressed_ShouldSubmitTheForm', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<ProfileAccount />);

  await view.getByTestId(ids.passwordCurrent).fill('old-password-1');
  await view.getByTestId(ids.passwordNew).fill('a-new-password');
  await view.getByTestId(ids.passwordConfirm).fill('a-new-password');
  await view.getByTestId(ids.passwordConfirm).press('Enter');

  await expect.poll(() => calls.list).toEqual(['POST /api/auth/password']);
});

test('Password_WhenChecksUnmetAndEnterPressed_ShouldPostNothing', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<ProfileAccount />);

  await view.getByTestId(ids.passwordNew).fill('short');
  await view.getByTestId(ids.passwordNew).press('Enter');

  await expect(view.getByTestId(ids.passwordSubmit)).toBeDisabled();
  expect(calls.list).toEqual([]);
});

test('Password_ShouldDeclareAutocompleteForPasswordManagers', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfileAccount />);

  await expect(view.getByTestId(ids.passwordUsername)).toHaveAttribute('autocomplete', 'username');
  await expect(view.getByTestId(ids.passwordUsername)).toHaveValue('priya@example.test');
  await expect(view.getByTestId(ids.passwordCurrent)).toHaveAttribute(
    'autocomplete',
    'current-password',
  );
  await expect(view.getByTestId(ids.passwordNew)).toHaveAttribute('autocomplete', 'new-password');
  await expect(view.getByTestId(ids.passwordConfirm)).toHaveAttribute(
    'autocomplete',
    'new-password',
  );
});

test('Password_ShouldSayAgentsAreDisconnected', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfileAccount />);

  await expect(view.getByTestId(ids.passwordPanel)).toContainText(
    'disconnects your connected agents',
  );
  await expect(view.getByTestId(ids.sessionsPanel)).toContainText(
    'disconnects your connected agents',
  );
});

test('Appearance_WhenFirstPickRefusedAfterSecondSaved_ShouldKeepTheSecond', async ({
  mount,
  page,
}) => {
  let releaseDark: () => void = () => undefined;
  const darkHeld = new Promise<void>((resolve) => (releaseDark = resolve));
  const writes: string[] = [];
  let saved = me.theme;
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    if (request.method() === 'GET') {
      const isMe = new URL(request.url()).pathname === '/api/me';
      return route.fulfill({ json: isMe ? { ...me, theme: saved } : [] });
    }
    const value = request.postDataJSON() as string;
    writes.push(value);
    if (value === 'dark') {
      await darkHeld;
      return route.fulfill({ status: 400, json: { code: 'validation', error: 'Not saved.' } });
    }
    saved = value as typeof saved;
    return route.fulfill({ status: 204 });
  });
  const view = await mount(<ProfileAccount />);
  const pick = (value: string) =>
    view.locator(`[data-testid="${ids.themeOption}"][data-value="${value}"]`);

  await pick('dark').click();
  await pick('light').click();
  await expect(pick('light')).toHaveAttribute('aria-pressed', 'true');
  expect(writes).toEqual(['dark']);
  releaseDark();

  await expect(view.getByTestId(ids.appearanceError)).toHaveText('Not saved.');
  await expect.poll(() => writes).toEqual(['dark', 'light']);
  await expect(pick('light')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
});

test('Projects_WhenLeaveOpened_ShouldMoveFocusToCancel', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfileAccount />);

  await view.locator(`[data-key="PAY"] [data-testid="${ids.myProjectLeave}"]`).click();

  await expect(view.getByTestId(ids.myProjectLeaveCancel)).toBeFocused();
});

test('Projects_WhenLeaveDone_ShouldFocusTheList', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfileAccount />);

  await view.locator(`[data-key="PAY"] [data-testid="${ids.myProjectLeave}"]`).click();
  await view.getByTestId(ids.myProjectLeaveConfirm).click();

  await expect(view.getByTestId(ids.myProjectRow)).toHaveCount(1);
  await expect(view.getByTestId(ids.myProjectsList)).toBeFocused();
});

test('Sessions_WhenSignOutOfThisBrowserClicked_ShouldPostLogout', async ({ mount, page }) => {
  const calls = await stub(page);
  await page.route('**/login?state=signed-out', (route) =>
    route.fulfill({ status: 200, contentType: 'text/html', body: '<p>signed out</p>' }),
  );
  const view = await mount(<ProfileAccount />);

  await view.getByTestId(ids.signOutBrowser).click();

  await expect.poll(() => calls.list).toContain('POST /api/auth/logout');
  // Wait for the redirect, or it lands in the next test of this worker.
  await page.waitForURL('**/login?state=signed-out');
});

const proxyInstance = (page: Routable, signOutUrl: string | null) =>
  page.route('**/api/auth/instance', (route) =>
    route.fulfill({ json: { name: 'ShipLean', signIn: 'proxy', signOutUrl } }),
  );

test('Account_WhenProxyMode_ShouldHideThePasswordPanelAndKeepSignOutOthers', async ({
  mount,
  page,
}) => {
  await stub(page);
  await proxyInstance(page, '/oauth2/sign_out');
  const view = await mount(<ProfileAccount />);

  await expect(view.getByTestId(ids.signOutOthers)).toBeVisible();
  await expect(view.getByTestId(ids.passwordPanel)).toHaveCount(0);
});

test('Sessions_WhenProxyModeSignOutClicked_ShouldPostLogoutThenGoToTheSignOutUrl', async ({
  mount,
  page,
}) => {
  const calls = await stub(page);
  await proxyInstance(page, '/oauth2/sign_out');
  await page.route('**/oauth2/sign_out', (route) =>
    route.fulfill({ status: 200, contentType: 'text/html', body: '<p>proxy signed out</p>' }),
  );
  const view = await mount(<ProfileAccount />);
  await expect(view.getByTestId(ids.passwordPanel)).toHaveCount(0);

  await view.getByTestId(ids.signOutBrowser).click();

  await expect.poll(() => calls.list).toContain('POST /api/auth/logout');
  await page.waitForURL('**/oauth2/sign_out');
});

test('Sessions_WhenProxyModeWithoutSignOutUrl_ShouldNotOfferSignOutOfThisBrowser', async ({
  mount,
  page,
}) => {
  await stub(page);
  await proxyInstance(page, null);
  const view = await mount(<ProfileAccount />);

  await expect(view.getByTestId(ids.passwordPanel)).toHaveCount(0);
  await expect(view.getByTestId(ids.signOutOthers)).toBeVisible();
  await expect(view.getByTestId(ids.signOutBrowser)).toHaveCount(0);
});
