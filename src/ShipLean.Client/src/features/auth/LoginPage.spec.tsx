import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { componentTestIds } from '../../components/testIds';
import { appTestIds } from '../../testIds';
import {
  meDto,
  projectSummary,
  signInWithPassword,
  stubInstance,
  stubJson,
  stubProxyInstance,
} from './authFixtures';
import { authTestIds as ids } from './testIds';

test('LoginPage_WhenOpened_ShouldShowTheInstanceNameAndTheFooter', async ({ mount, page }) => {
  await stubInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await expect(page.getByTestId(ids.instanceName)).toHaveText(/Larkspur/);
  await expect(page.getByTestId(ids.signInForm)).toContainText('Ask your ShipLean admin');
});

test('LoginPage_WhenReturnUrlIsExternal_ShouldIgnoreIt', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/auth/login', meDto());
  await stubJson(page, '/api/me', meDto());
  await stubJson(page, '/api/projects', [projectSummary('SHIP'), projectSummary('PAY')]);
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/login?returnUrl=https%3A%2F%2Fevil.example%2Fp%2FPAY' },
  });
  await signInWithPassword(page);
  await expect(page.getByTestId(appTestIds.shellTopBar)).toContainText('SHIP');
});

test('LoginPage_WhenReturnUrlIsLocal_ShouldOpenIt', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/auth/login', meDto());
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/login?returnUrl=%2Fp%2FPAY%2Fbacklogs%2Fstories' },
  });
  await signInWithPassword(page);
  await expect(page.getByTestId(appTestIds.shellTopBar)).toContainText('PAY');
});

test('LoginPage_WhenSignInFails_ShouldShowTheServersSentence', async ({ mount, page }) => {
  await stubInstance(page);
  // 429, not 401: a 401 makes the API client navigate the test page to /login.
  await stubJson(
    page,
    '/api/auth/login',
    {
      error: 'Incorrect email or password, or the account is locked for now.',
      code: 'rate_limited',
    },
    429,
  );
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await signInWithPassword(page);
  await expect(page.getByTestId(ids.failure)).toHaveText(
    'Incorrect email or password, or the account is locked for now.',
  );
  await expect(page.getByTestId(ids.password)).toHaveValue('');
  await expect(page.getByTestId(ids.password)).toBeFocused();
});

test('LoginPage_WhenStateIsEnded_ShouldShowTheNoteAboveTheForm', async ({ mount, page }) => {
  await stubInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=ended' } });
  await expect(page.getByTestId(ids.note)).toContainText('password was changed or reset');
  await expect(page.getByTestId(ids.signInForm)).toBeVisible();
});

test('LoginPage_WhenStateIsSignedOut_ShouldShowTheNote', async ({ mount, page }) => {
  await stubInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=signed-out' } });
  await expect(page.getByTestId(ids.note)).toContainText("You're signed out");
});

test('LoginPage_WhenOneTimePassword_ShouldAskForANewPasswordBeforeAnythingElse', async ({
  mount,
  page,
}) => {
  await stubInstance(page);
  await stubJson(page, '/api/auth/login', meDto({ status: 'oneTimePassword' }));
  let changeBody: unknown = null;
  await page.route('**/api/auth/password', (route) => {
    changeBody = route.request().postDataJSON();
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(meDto()),
    });
  });
  await stubJson(page, '/api/me', meDto());
  await stubJson(page, '/api/projects', [projectSummary('PAY')]);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await signInWithPassword(page);

  await expect(page.getByRole('heading', { name: 'Choose your own password' })).toBeVisible();
  await expect(page.getByTestId(ids.changeSubmit)).toBeDisabled();
  await page.getByTestId(ids.newPassword).fill('a-new-password');
  await page.getByTestId(ids.confirmPassword).fill('a-new-password');
  await expect(page.getByTestId(ids.changeSubmit)).toBeEnabled();
  await page.getByTestId(ids.changeSubmit).click();

  await expect(page.getByTestId(appTestIds.shellTopBar)).toContainText('PAY');
  expect(changeBody).toEqual({ current: 'a-password', new: 'a-new-password' });
});

test('LoginPage_WhenNewPasswordIsTheOneTimePassword_ShouldNotAllowSaving', async ({
  mount,
  page,
}) => {
  await stubInstance(page);
  await stubJson(page, '/api/auth/login', meDto({ status: 'oneTimePassword' }));
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await signInWithPassword(page);

  await page.getByTestId(ids.newPassword).fill('a-password');
  await page.getByTestId(ids.confirmPassword).fill('a-password');
  await expect(page.getByTestId(ids.changeSubmit)).toBeDisabled();
});

test('LoginPage_WhenChangeStateArrivesWithoutPassword_ShouldSignInFirst', async ({
  mount,
  page,
}) => {
  await stubInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=change' } });
  await expect(page.getByTestId(ids.signInForm)).toBeVisible();
  await expect(page.getByTestId(ids.note)).toContainText('one-time password');
  await expect(page.getByTestId(ids.changeForm)).toHaveCount(0);
});

test('LoginPage_WhenNoProjects_ShouldOfferNewProjectAndImport', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/me', meDto({ hasProjects: false }));
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=no-projects' } });
  await expect(page.getByTestId(ids.noProjects)).toContainText("You're not in any projects yet");
  await expect(page.getByTestId(ids.newProject)).toBeVisible();
  await expect(page.getByTestId(ids.importFromJira)).toHaveAttribute('href', '/import');
  await expect(page.getByTestId(ids.signOut)).toBeVisible();
});

test('LoginPage_WhenCannotCreate_ShouldHideNewProjectAndImport', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/me', meDto({ hasProjects: false, canCreateProjects: false }));
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=no-projects' } });
  await expect(page.getByTestId(ids.noProjects)).toBeVisible();
  await expect(page.getByTestId(ids.newProject)).toHaveCount(0);
  await expect(page.getByTestId(ids.importFromJira)).toHaveCount(0);
});

test('LoginPage_WhenOpened_ShouldTitleThePageWithTheInstanceName', async ({ mount, page }) => {
  await stubInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await expect(page).toHaveTitle('Sign in · Larkspur');
});

test('LoginPage_WhenSubmittedWithAnEmptyField_ShouldFocusTheFirstEmptyOne', async ({
  mount,
  page,
}) => {
  await stubInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await page.getByTestId(ids.signInSubmit).click();
  await expect(page.getByTestId(ids.email)).toBeFocused();
  await page.getByTestId(ids.email).fill('priya@shiplean.local');
  await page.getByTestId(ids.signInSubmit).click();
  await expect(page.getByTestId(ids.password)).toBeFocused();
});

test('LoginPage_WhenSignedIn_ShouldToastWhereItIsOpening', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/auth/login', meDto());
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/login?returnUrl=%2Fp%2FPAY%2Fbacklogs%2Fstories' },
  });
  await signInWithPassword(page);
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Signed in. Opening');
});

test('LoginPage_WhenPasswordSaved_ShouldToastIt', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/auth/login', meDto({ status: 'oneTimePassword' }));
  await stubJson(page, '/api/auth/password', meDto());
  await stubJson(page, '/api/me', meDto());
  await stubJson(page, '/api/projects', [projectSummary('PAY')]);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await signInWithPassword(page);
  await page.getByTestId(ids.newPassword).fill('a-new-password');
  await page.getByTestId(ids.confirmPassword).fill('a-new-password');
  await page.getByTestId(ids.changeSubmit).click();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Password saved');
});

test('LoginPage_WhenSignOutInsteadOnTheChangeStep_ShouldPostLogout', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/auth/login', meDto({ status: 'oneTimePassword' }));
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await signInWithPassword(page);
  const logout = page.waitForRequest(
    (request) => request.url().endsWith('/api/auth/logout') && request.method() === 'POST',
  );
  await page.route('**/api/auth/logout', (route) => route.fulfill({ status: 204 }));
  await page.route('**/login?state=signed-out', (route) =>
    route.fulfill({ status: 200, contentType: 'text/html', body: '<html></html>' }),
  );
  await page.getByTestId(ids.signOut).click();
  await logout;
  await page.waitForURL('**/login?state=signed-out');
});

test('LoginPage_WhenSignOutOnTheNoProjectsCard_ShouldPostLogout', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/me', meDto({ hasProjects: false }));
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=no-projects' } });
  const logout = page.waitForRequest(
    (request) => request.url().endsWith('/api/auth/logout') && request.method() === 'POST',
  );
  await page.route('**/api/auth/logout', (route) => route.fulfill({ status: 204 }));
  await page.route('**/login?state=signed-out', (route) =>
    route.fulfill({ status: 200, contentType: 'text/html', body: '<html></html>' }),
  );
  await page.getByTestId(ids.signOut).click();
  await logout;
  await page.waitForURL('**/login?state=signed-out');
});

// The CT page is not served at /login, where the API client skips its redirect on a 401. Make it look like /login, so
// the 401 tests prove that skip.
const atLoginAddress = (page: Parameters<typeof stubInstance>[0]) =>
  (page as unknown as { evaluate(script: () => void): Promise<void> }).evaluate(() =>
    window.history.replaceState(null, '', '/login'),
  );

const signedOutPage = (page: Parameters<typeof stubInstance>[0], url: string) =>
  page.route(url, (route) =>
    route.fulfill({ status: 200, contentType: 'text/html', body: '<html></html>' }),
  );

test('LoginPage_WhenProxyModeWithoutAnIdentity_ShouldShowTheProxyMessageAndNoForm', async ({
  mount,
  page,
}) => {
  await atLoginAddress(page);
  await stubProxyInstance(page);
  await stubJson(page, '/api/me', { error: 'Sign in.', code: 'unauthorized' }, 401);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await expect(page.getByTestId(ids.proxyMessage)).toContainText(
    'no sign-in reached ShipLean. Open ShipLean through its usual address.',
  );
  await expect(page.getByTestId(ids.password)).toHaveCount(0);
  await expect(page.getByTestId(ids.signInSubmit)).toHaveCount(0);
  // The 401 on the sign-in page must not navigate anywhere.
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByTestId(ids.proxyRetry)).toBeVisible();
});

test('LoginPage_WhenProxyModeAndStateIsDisabled_ShouldSayTheAccountIsDisabled', async ({
  mount,
  page,
}) => {
  const meRequests: string[] = [];
  page.on('request', (request) => {
    if (request.url().endsWith('/api/me')) meRequests.push(request.url());
  });
  await stubProxyInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=disabled' } });
  await expect(page.getByTestId(ids.proxyMessage)).toHaveText(
    'Your ShipLean account is disabled. Ask an instance admin to turn it back on.',
  );
  expect(meRequests).toEqual([]);
  await expect(page.getByTestId(ids.password)).toHaveCount(0);
});

test('LoginPage_WhenProxyModeAndStateIsEnded_ShouldSayTheSessionEnded', async ({ mount, page }) => {
  await atLoginAddress(page);
  await stubProxyInstance(page);
  await stubJson(page, '/api/me', { error: 'Sign in.', code: 'unauthorized' }, 401);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=ended' } });
  await expect(page.getByTestId(ids.proxyMessage)).toContainText('Your session ended. Reload');
  await expect(page.getByTestId(ids.password)).toHaveCount(0);
});

test('LoginPage_WhenProxyModeAndSignedIn_ShouldGoToTheReturnUrl', async ({ mount, page }) => {
  await stubProxyInstance(page);
  await stubJson(page, '/api/me', meDto());
  await stubJson(page, '/api/projects', [projectSummary('PAY')]);
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/login?returnUrl=%2Fp%2FPAY%2Fbacklogs%2Fstories' },
  });
  await expect(page.getByTestId(appTestIds.shellTopBar)).toContainText('PAY');
  await expect(page.getByTestId(ids.proxyMessage)).toHaveCount(0);
});

test('LoginPage_WhenProxyModeAndStateIsSignedOutButSignedIn_ShouldGoOn', async ({
  mount,
  page,
}) => {
  await stubProxyInstance(page);
  await stubJson(page, '/api/me', meDto());
  await stubJson(page, '/api/projects', [projectSummary('SHIP')]);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=signed-out' } });
  await expect(page.getByTestId(appTestIds.shellTopBar)).toContainText('SHIP');
});

test('LoginPage_WhenProxyModeAndReturnUrlIsExternal_ShouldGoToTheRoot', async ({ mount, page }) => {
  await stubProxyInstance(page);
  await stubJson(page, '/api/me', meDto());
  await stubJson(page, '/api/projects', [projectSummary('SHIP'), projectSummary('PAY')]);
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/login?returnUrl=https%3A%2F%2Fevil.example%2Fp%2FPAY' },
  });
  await expect(page.getByTestId(appTestIds.shellTopBar)).toContainText('SHIP');
});

test('LoginPage_WhenProxyModeAndReturnUrlIsAServerPath_ShouldDoAFullLoad', async ({
  mount,
  page,
}) => {
  await stubProxyInstance(page);
  await stubJson(page, '/api/me', meDto());
  await signedOutPage(page, '**/oauth/authorize**');
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/login?returnUrl=%2Foauth%2Fauthorize%3Fclient_id%3Dabc' },
  });
  await page.waitForURL('**/oauth/authorize?client_id=abc');
});

test('LoginPage_WhenProxyModeAndTryAgainClicked_ShouldLoadTheReturnUrlFresh', async ({
  mount,
  page,
}) => {
  await atLoginAddress(page);
  await stubProxyInstance(page);
  await stubJson(page, '/api/me', { error: 'Sign in.', code: 'unauthorized' }, 401);
  await signedOutPage(page, '**/p/PAY/backlogs/stories');
  await mount(<AppRoutes />, {
    hooksConfig: { route: '/login?returnUrl=%2Fp%2FPAY%2Fbacklogs%2Fstories' },
  });
  await page.getByTestId(ids.proxyRetry).click();
  await page.waitForURL('**/p/PAY/backlogs/stories');
});

test('LoginPage_WhenProxyModeAndNoProjects_ShouldShowTheCardAndSignOutToTheProxy', async ({
  mount,
  page,
}) => {
  await stubProxyInstance(page, '/oauth2/sign_out');
  await stubJson(page, '/api/me', meDto({ hasProjects: false }));
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=no-projects' } });
  await expect(page.getByTestId(ids.noProjects)).toContainText("You're not in any projects yet");
  await expect(page.getByTestId(ids.proxyMessage)).toHaveCount(0);
  const logout = page.waitForRequest(
    (request) => request.url().endsWith('/api/auth/logout') && request.method() === 'POST',
  );
  await page.route('**/api/auth/logout', (route) => route.fulfill({ status: 204 }));
  await signedOutPage(page, '**/oauth2/sign_out');
  await page.getByTestId(ids.signOut).click();
  await logout;
  await page.waitForURL('**/oauth2/sign_out');
});

test('LoginPage_WhenProxyModeNoProjectsAndNoSignOutUrl_ShouldHideSignOut', async ({
  mount,
  page,
}) => {
  await stubProxyInstance(page);
  await stubJson(page, '/api/me', meDto({ hasProjects: false }));
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=no-projects' } });
  await expect(page.getByTestId(ids.noProjects)).toBeVisible();
  await expect(page.getByTestId(ids.signOut)).toHaveCount(0);
});

test('LoginPage_WhenInstanceReadFails_ShouldShowThePasswordForm', async ({ mount, page }) => {
  await stubJson(page, '/api/auth/instance', { error: 'Nope.', code: 'internal' }, 500);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await expect(page.getByTestId(ids.signInForm)).toBeVisible();
});
