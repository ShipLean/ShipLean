import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import {
  meDto,
  signInWithPassword,
  stubGoogleInstance,
  stubInstance,
  stubJson,
  stubProxyInstance,
} from './authFixtures';
import { authTestIds as ids } from './testIds';

// Sign in, Choose your own password, No projects yet and proxy sign-in on a phone (390×844, SHIP-736 L736a), as page
// shots. Screenshots are compared only in CI; the assertions around them are what fail locally.
// Regenerate with scripts/update-page-shots.sh src/features/auth/LoginPage.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;

const phoneOnly = (page: { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

async function shoot(
  page: Parameters<typeof phoneOnly>[0] & {
    emulateMedia(options: {
      colorScheme: 'light' | 'dark';
      reducedMotion: 'reduce';
    }): Promise<void>;
  },
  theme: (typeof THEMES)[number],
) {
  phoneOnly(page);
  await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
}

for (const theme of THEMES) {
  test(`SignInPhone_WhenGoogleIsOn_ShouldMatchItsScreenshot_sign-in_${theme}`, async ({
    mount,
    page,
  }) => {
    await shoot(page, theme);
    await stubGoogleInstance(page);
    await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=signed-out' } });
    await expect(page.getByTestId(ids.orWithEmail)).toBeVisible();
    await expect(page).toHaveScreenshot(`sign-in.${theme}.png`, { animations: 'disabled' });
  });

  test(`SignInPhone_WhenPasswordOnly_ShouldMatchItsScreenshot_sign-in-password-only_${theme}`, async ({
    mount,
    page,
  }) => {
    await shoot(page, theme);
    await stubInstance(page);
    await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
    await expect(page.getByTestId(ids.signInSubmit)).toBeVisible();
    await expect(page).toHaveScreenshot(`sign-in-password-only.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`SignInPhone_WhenTheSignInFails_ShouldMatchItsScreenshot_sign-in-error_${theme}`, async ({
    mount,
    page,
  }) => {
    await shoot(page, theme);
    await stubInstance(page);
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
    await expect(page.getByTestId(ids.failure)).toBeVisible();
    await expect(page).toHaveScreenshot(`sign-in-error.${theme}.png`, { animations: 'disabled' });
  });

  test(`SignInPhone_WhenChoosingAPassword_ShouldMatchItsScreenshot_sign-in-change_${theme}`, async ({
    mount,
    page,
  }) => {
    await shoot(page, theme);
    await stubInstance(page);
    await stubJson(page, '/api/auth/login', meDto({ status: 'oneTimePassword' }));
    await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
    await signInWithPassword(page);
    await page.getByTestId(ids.newPassword).fill('a-new-password');
    await page.getByTestId(ids.confirmPassword).fill('a-new');
    await expect(page.getByTestId(ids.changeSubmit)).toBeVisible();
    await expect(page).toHaveScreenshot(`sign-in-change.${theme}.png`, { animations: 'disabled' });
  });

  test(`SignInPhone_WhenInNoProjects_ShouldMatchItsScreenshot_sign-in-no-projects_${theme}`, async ({
    mount,
    page,
  }) => {
    await shoot(page, theme);
    await stubInstance(page);
    await stubJson(page, '/api/me', meDto({ hasProjects: false }));
    await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=no-projects' } });
    await expect(page.getByTestId(ids.newProject)).toBeVisible();
    await expect(page).toHaveScreenshot(`sign-in-no-projects.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`SignInPhone_WhenBehindAProxy_ShouldMatchItsScreenshot_sign-in-proxy_${theme}`, async ({
    mount,
    page,
  }) => {
    await shoot(page, theme);
    // The CT page is not served at /login, where the API client skips its redirect on a 401.
    await page.evaluate(() => window.history.replaceState(null, '', '/login'));
    await stubProxyInstance(page);
    await stubJson(page, '/api/me', { error: 'Sign in.', code: 'unauthorized' }, 401);
    await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
    await expect(page.getByTestId(ids.proxyRetry)).toBeVisible();
    await expect(page).toHaveScreenshot(`sign-in-proxy.${theme}.png`, { animations: 'disabled' });
  });
}

test('SignInPhone_WhenGoogleIsOn_ShouldPutGoogleBeforeTheFields', async ({ mount, page }) => {
  phoneOnly(page);
  await stubGoogleInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login?state=ended' } });
  const top = async (id: string) => (await page.getByTestId(id).boundingBox())!.y;
  const order = [
    ids.note,
    ids.googleSignIn,
    ids.orWithEmail,
    ids.email,
    ids.password,
    ids.signInSubmit,
  ];
  const tops = await Promise.all(order.map(top));
  expect(tops).toEqual([...tops].sort((a, b) => a - b));
  expect(new Set(tops).size).toBe(order.length);
  const google = (await page.getByTestId(ids.googleSignIn).boundingBox())!;
  const card = (await page.getByTestId(ids.signInForm).boundingBox())!;
  expect(google.height).toBe(40);
  expect(google.width).toBe(card.width - 34);
});

test('SignInPhone_WhenNoGoogle_ShouldPutTheFieldsRightUnderTheHeading', async ({ mount, page }) => {
  phoneOnly(page);
  await stubInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await expect(page.getByTestId(ids.googleSignIn)).toHaveCount(0);
  await expect(page.getByTestId(ids.orWithEmail)).toHaveCount(0);
  const email = (await page.getByTestId(ids.email).boundingBox())!;
  const submit = (await page.getByTestId(ids.signInSubmit).boundingBox())!;
  expect(email.height).toBe(40);
  expect(submit.height).toBe(40);
  expect(submit.y).toBeGreaterThan(email.y);
});

test('SignInPhone_WhenAnInstanceIsCalledShipLean_ShouldShowOnlyTheMark', async ({
  mount,
  page,
}) => {
  phoneOnly(page);
  await stubJson(page, '/api/auth/instance', { name: 'ShipLean' });
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await expect(page.getByTestId(ids.signInForm)).toBeVisible();
  await expect(page.getByTestId(ids.topBar)).toContainText('ShipLean');
  await expect(page.getByTestId(ids.instanceName)).toHaveCount(0);
});

test('SignInPhone_WhenTheInstanceNameIsLong_ShouldCutItInTheBar', async ({ mount, page }) => {
  phoneOnly(page);
  await stubJson(page, '/api/auth/instance', { name: 'Larkspur '.repeat(12).trim() });
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  const name = page.getByTestId(ids.instanceName);
  await expect(name).toBeVisible();
  expect(await name.evaluate((el) => el.scrollWidth > el.clientWidth)).toBe(true);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(390);
});

test('SignInPhone_WhenOpened_ShouldNotFocusEmailAndShouldSizeTheFields', async ({
  mount,
  page,
}) => {
  phoneOnly(page);
  await stubInstance(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await expect(page.getByTestId(ids.signInForm)).toBeVisible();
  await expect(page.getByTestId(ids.email)).not.toBeFocused();
  const bar = (await page.getByTestId(ids.topBar).boundingBox())!;
  const card = (await page.getByTestId(ids.signInForm).boundingBox())!;
  expect(bar.height).toBe(48);
  expect(card.y).toBe(bar.height + 16);
  expect(card.width).toBe(358);
  const reveal = (await page.getByTestId(ids.revealPassword).boundingBox())!;
  expect(reveal.height).toBeGreaterThanOrEqual(32);
  for (const id of [ids.email, ids.password]) {
    expect(await page.getByTestId(id).evaluate((el) => getComputedStyle(el).fontSize)).toBe('16px');
  }
});

test('SignInPhone_WhenChoosingAPassword_ShouldStackTheChecksAndSaveUnderThem', async ({
  mount,
  page,
}) => {
  phoneOnly(page);
  await stubInstance(page);
  await stubJson(page, '/api/auth/login', meDto({ status: 'oneTimePassword' }));
  await mount(<AppRoutes />, { hooksConfig: { route: '/login' } });
  await signInWithPassword(page);
  await expect(page.getByTestId(ids.newPassword)).not.toBeFocused();
  const confirm = (await page.getByTestId(ids.confirmPassword).boundingBox())!;
  const checks = (await page.getByTestId(ids.passwordChecks).boundingBox())!;
  const save = (await page.getByTestId(ids.changeSubmit).boundingBox())!;
  const signOut = (await page.getByTestId(ids.signOut).boundingBox())!;
  expect(checks.x).toBe(confirm.x);
  expect(checks.y).toBeGreaterThan(confirm.y + confirm.height);
  expect(save.y).toBeGreaterThan(checks.y + checks.height);
  expect(save.width).toBe(confirm.width);
  expect(save.height).toBe(40);
  expect(signOut.height).toBeGreaterThanOrEqual(32);
  expect(signOut.y).toBeGreaterThan(save.y);
});
