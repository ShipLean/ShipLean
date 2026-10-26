import { expect, test } from '@playwright/experimental-ct-react';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import { settingsTestIds as ids } from '../testIds';
import { InstanceUsers } from './InstanceUsers';
import type { AdminUserDto, AdminUsersDto } from './usersAdminApi';

const user = (id: string, name: string, over: Partial<AdminUserDto> = {}): AdminUserDto => ({
  id,
  name,
  email: `${name.split(' ')[0].toLowerCase()}@example.test`,
  initials: name
    .split(' ')
    .map((w) => w[0])
    .join(''),
  avatarColour: '#52627c',
  photoUrl: null,
  isInstanceAdmin: false,
  status: 'active',
  lastSignIn: '2 days ago',
  projects: [],
  ...over,
});

const ship = { id: 'p-ship', key: 'SHIP', name: 'Storefront' };

const initial = (): AdminUsersDto => ({
  users: [
    user('me', 'Joel Dickson', {
      isInstanceAdmin: true,
      projects: [{ ...ship, role: 'admin', soleActiveAdmin: true }],
    }),
    user('lk', 'Locked Larry', { status: 'locked' }),
    user('np', 'Imported Ines', { status: 'noPassword', lastSignIn: null }),
    user('off', 'Disabled Dan', { status: 'disabled' }),
    user('sa', 'Sole Sue', { projects: [{ ...ship, role: 'admin', soleActiveAdmin: true }] }),
    user('ok', 'Shared Sam', { projects: [{ ...ship, role: 'admin', soleActiveAdmin: false }] }),
  ],
  administeredProjects: [ship],
});

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; url(): string; postDataJSON(): unknown };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

interface StubOptions {
  proxy?: boolean;
  /** Add user answers 409 with this message instead of adding. */
  addConflict?: boolean;
  /** Add user answers a null one-time password (proxy mode). */
  noPassword?: boolean;
}

const stub = async (page: Routable, options: StubOptions = {}) => {
  const calls: string[] = [];
  const state = initial();
  await page.route('**/api/auth/instance', (route) =>
    route.fulfill({
      json: options.proxy
        ? { name: 'Larkspur', signIn: 'proxy', signOutUrl: null }
        : { name: 'Larkspur', signIn: 'password', signOutUrl: null },
    }),
  );
  await page.route('**/api/me', (route) => route.fulfill({ json: { id: 'me' } }));
  await page.route('**/api/admin/users**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === 'GET') return route.fulfill({ json: state });
    calls.push(`${request.method()} ${path}`);
    const target = state.users.find((u) => path.includes(u.id));
    if (path.endsWith('/one-time-password'))
      return route.fulfill({ json: { userId: target?.id, password: 'ABCD-EFGH-JKLM' } });
    if (path.endsWith('/unlock') && target) target.status = 'active';
    if (path.endsWith('/disabled') && target) target.status = 'active';
    if (path.endsWith('/instance-admin') && target)
      target.isInstanceAdmin = (
        request.postDataJSON() as { isInstanceAdmin: boolean }
      ).isInstanceAdmin;
    if (path.endsWith('/email') && target)
      target.email = (request.postDataJSON() as { email: string }).email;
    if (request.method() === 'POST' && path === '/api/admin/users' && options.addConflict)
      return route.fulfill({
        status: 409,
        json: { code: 'conflict', error: 'That email is already taken.' },
      });
    if (request.method() === 'POST' && path === '/api/admin/users') {
      const body = request.postDataJSON() as { name: string; email: string };
      const added = user('new', body.name, { status: 'oneTimePassword', email: body.email });
      state.users.push(added);
      return route.fulfill({
        json: {
          user: added,
          oneTimePassword: options.noPassword
            ? null
            : { userId: 'new', password: 'WXYZ-2345-6789' },
        },
      });
    }
    return route.fulfill({ status: 204 });
  });
  return calls;
};

test('Users_WhenLoaded_ShouldShowStatusProjectsAndLastSignIn', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceUsers />);

  await expect(view.getByTestId(ids.usersRow)).toHaveCount(6);
  const me = view.getByTestId(ids.usersRow).first();
  await expect(me.getByTestId(ids.usersProjects)).toHaveText('SHIP');
  await expect(me.getByTestId(ids.usersName)).toContainText('(you)');
  await expect(view.getByTestId(ids.usersRow).nth(2).getByTestId(ids.usersLastSignIn)).toHaveText(
    'Never',
  );
  await expect(view.getByTestId(ids.usersAttentionBanner)).toContainText('2 people need you');
});

test('Users_WhenSearchedAndFiltered_ShouldNarrowTheTable', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersSearch).fill('locked@');
  await expect(view.getByTestId(ids.usersRow)).toHaveCount(1);
  await view.getByTestId(ids.usersSearch).fill('');
  await view.getByTestId(ids.usersFilterDisabled).click();
  await expect(view.getByTestId(ids.usersRow)).toHaveCount(1);
  await expect(view.getByTestId(ids.usersRow)).toContainText('Disabled Dan');
});

test('Users_WhenOwnRow_ShouldRefuseDisableAndDemote', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersRow).first().getByTestId(ids.usersActions).click();

  await expect(view.getByTestId(ids.usersMenuDisable)).toBeDisabled();
  await expect(view.getByTestId(ids.usersMenuAdmin)).toBeDisabled();
});

test('Users_WhenOwnRow_ShouldRefuseResetAndEmailChange', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersRow).first().getByTestId(ids.usersActions).click();

  await expect(view.getByTestId(ids.usersMenuPassword)).toBeDisabled();
  await expect(view.getByTestId(ids.usersMenuEmail)).toBeDisabled();
  await expect(view.getByTestId(ids.usersMenuPassword)).toContainText('Your profile');
  expect(calls).toEqual([]);
});

test('Users_WhenResettingAnActiveUser_ShouldConfirmThenShowThePasswordOnce', async ({
  mount,
  page,
}) => {
  const calls = await stub(page);
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersRow).nth(5).getByTestId(ids.usersActions).click();
  await view.getByTestId(ids.usersMenuPassword).click();
  await expect(page.getByTestId(ids.usersDialogConfirm)).toHaveText('Reset password');
  expect(calls).toEqual([]);
  await page.getByTestId(ids.usersDialogConfirm).click();

  await expect(page.getByTestId(componentTestIds.otpPassword)).toHaveText('ABCD-EFGH-JKLM');
  expect(calls).toEqual(['POST /api/admin/users/ok/one-time-password']);
  await page.getByTestId(componentTestIds.otpDone).click();
  await expect(page.getByTestId(componentTestIds.otpPassword)).toHaveCount(0);
});

test('Users_WhenEmailChanged_ShouldSendTheNewAddressAndShowIt', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersRow).nth(5).getByTestId(ids.usersActions).click();
  await view.getByTestId(ids.usersMenuEmail).click();
  await page.getByTestId(ids.usersEmailInput).fill('sam.new@example.test');
  await page.getByTestId(ids.usersDialogConfirm).click();

  await expect(page.getByTestId(ids.usersEmailInput)).toHaveCount(0);
  await expect(view.getByTestId(ids.usersRow).nth(5).getByTestId(ids.usersEmail)).toHaveText(
    'sam.new@example.test',
  );
  expect(calls).toEqual(['PUT /api/admin/users/ok/email']);
});

test('Users_WhenAdminRightsToggled_ShouldPromoteThenDemote', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersRow).nth(5).getByTestId(ids.usersActions).click();
  await expect(view.getByTestId(ids.usersMenuAdmin)).toHaveText('Make admin');
  await view.getByTestId(ids.usersMenuAdmin).click();
  await expect(view.getByTestId(ids.usersRow).nth(5).getByTestId(ids.usersRole)).toHaveText(
    'Admin',
  );
  await view.getByTestId(ids.usersRow).nth(5).getByTestId(ids.usersActions).click();
  await expect(view.getByTestId(ids.usersMenuAdmin)).toHaveText('Remove admin');
  await view.getByTestId(ids.usersMenuAdmin).click();

  await expect(view.getByTestId(ids.usersRow).nth(5).getByTestId(ids.usersRole)).not.toHaveText(
    'Admin',
  );
  expect(calls).toEqual([
    'PUT /api/admin/users/ok/instance-admin',
    'PUT /api/admin/users/ok/instance-admin',
  ]);
});

test('Users_WhenSetUpSignIn_ShouldShowThePasswordOnceAfterConfirming', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersRow).nth(2).getByTestId(ids.usersActions).click();
  await view.getByTestId(ids.usersMenuPassword).click();
  expect(calls).toEqual([]);
  await page.getByTestId(ids.usersDialogConfirm).click();

  await expect(page.getByTestId(componentTestIds.otpPassword)).toHaveText('ABCD-EFGH-JKLM');
  await page.getByTestId(componentTestIds.otpDone).click();
  await expect(page.getByTestId(componentTestIds.otpPassword)).toHaveCount(0);
});

test('Users_WhenUnlocked_ShouldCallTheServer', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersRow).nth(1).getByTestId(ids.usersActions).click();
  await view.getByTestId(ids.usersMenuUnlock).click();

  await expect(view.getByTestId(ids.usersRow).nth(1).getByTestId(ids.usersStatus)).toHaveText(
    'Active',
  );
  expect(calls).toEqual(['POST /api/admin/users/lk/unlock']);
});

test('Users_WhenDisablingTheOnlyAdminOfAProject_ShouldWarnOnlyThen', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersRow).nth(4).getByTestId(ids.usersActions).click();
  await view.getByTestId(ids.usersMenuDisable).click();
  await expect(page.getByTestId(ids.usersDialogWarning)).toContainText(
    'SHIP would have no active admin',
  );
  await page.getByTestId(ids.usersDialogCancel).click();
  await view.getByTestId(ids.usersRow).nth(5).getByTestId(ids.usersActions).click();
  await view.getByTestId(ids.usersMenuDisable).click();
  await expect(page.getByTestId(ids.usersDialogWarning)).toHaveCount(0);
  await page.getByTestId(ids.usersDialogConfirm).click();

  await expect(page.getByTestId(ids.usersDialogConfirm)).toHaveCount(0);
  expect(calls).toEqual(['PUT /api/admin/users/ok/disabled']);
});

test('Users_WhenAdded_ShouldSendTheTickedProjectsAndRevealThePassword', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersAdd).click();
  await expect(page.getByTestId(ids.usersAddSubmit)).toBeDisabled();
  await page.getByTestId(ids.usersAddEmail).fill('new.person@example.test');
  await page.getByTestId(ids.usersAddName).fill('New Person');
  await page.getByTestId(ids.usersAddProject).check();
  await page.getByTestId(ids.usersAddSubmit).click();

  await expect(page.getByTestId(componentTestIds.otpPassword)).toHaveText('WXYZ-2345-6789');
  await page.getByTestId(componentTestIds.otpDone).click();
  await expect(view.getByTestId(ids.usersRow)).toHaveCount(7);
});

test('Users_WhenProxyMode_ShouldNotOfferPasswordSetUpOrEmailItems', async ({ mount, page }) => {
  await stub(page, { proxy: true });
  const view = await mount(<InstanceUsers />);

  for (const index of [0, 1, 2, 3, 4]) {
    await view.getByTestId(ids.usersRow).nth(index).getByTestId(ids.usersActions).click();
    await expect(page.getByTestId(ids.usersMenuPassword)).toHaveCount(0);
    await expect(page.getByTestId(ids.usersMenuEmail)).toHaveCount(0);
    await expect(page.getByTestId(ids.usersMenuDisable)).toHaveCount(1);
    await view.getByTestId(ids.usersRow).nth(index).getByTestId(ids.usersActions).click();
  }
  // Locked still offers Unlock, the admin toggle stays.
  await view.getByTestId(ids.usersRow).nth(1).getByTestId(ids.usersActions).click();
  await expect(page.getByTestId(ids.usersMenuUnlock)).toBeVisible();
  await expect(page.getByTestId(ids.usersMenuAdmin)).toBeVisible();
});

test('Users_WhenProxyModeAddedWithoutPassword_ShouldCloseWithoutThePasswordStep', async ({
  mount,
  page,
}) => {
  await stub(page, { proxy: true, noPassword: true });
  const view = await mount(
    <>
      <InstanceUsers />
      <ToastHost />
    </>,
  );

  await view.getByTestId(ids.usersAdd).click();
  await expect(page.getByTestId(ids.usersAddProxyNote)).toBeVisible();
  await page.getByTestId(ids.usersAddEmail).fill('new.person@example.test');
  await page.getByTestId(ids.usersAddName).fill('New Person');
  await page.getByTestId(ids.usersAddSubmit).click();

  await expect(page.getByTestId(ids.usersAddSubmit)).toHaveCount(0);
  await expect(page.getByTestId(componentTestIds.otpPassword)).toHaveCount(0);
  await expect(view.getByTestId(ids.usersRow)).toHaveCount(7);
  await expect(page.getByTestId(componentTestIds.toastHost)).toContainText('New Person is added');
});

test('Users_WhenProxyMode_ShouldNotShowTheAttentionBanner', async ({ mount, page }) => {
  await stub(page, { proxy: true });
  const view = await mount(<InstanceUsers />);

  await expect(view.getByTestId(ids.usersRow)).toHaveCount(6);
  await expect(view.getByTestId(ids.usersAttentionBanner)).toHaveCount(0);
});

test('Users_WhenAddIsRefusedWithAConflict_ShouldShowTheErrorInTheDialog', async ({
  mount,
  page,
}) => {
  await stub(page, { addConflict: true });
  const view = await mount(<InstanceUsers />);

  await view.getByTestId(ids.usersAdd).click();
  await expect(page.getByTestId(ids.usersAddProxyNote)).toHaveCount(0);
  await page.getByTestId(ids.usersAddEmail).fill('joel@example.test');
  await page.getByTestId(ids.usersAddName).fill('Joel Again');
  await page.getByTestId(ids.usersAddSubmit).click();

  await expect(page.getByTestId(ids.usersDialogError)).toContainText('already taken');
  await expect(page.getByTestId(ids.usersAddSubmit)).toBeVisible();
});
