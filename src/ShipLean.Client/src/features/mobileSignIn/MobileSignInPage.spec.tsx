import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { AppRoutes } from '../../AppRoutes';
import { meDto, stubInstance, stubJson, stubProxyInstance } from '../auth/authFixtures';
import { challenge, deviceName, queryFor, stateValue, stubCodes, validQuery } from './fixtures';
import { mobileSignInTestIds as ids } from './testIds';

const longDevice =
  'Priya’s Samsung Galaxy Z Fold6 (work profile, the one with the cracked outer screen)';

type Mount = (
  node: React.JSX.Element,
  options: { hooksConfig: { route: string } },
) => Promise<unknown>;

async function open(mount: Mount, page: Page, query = validQuery) {
  await stubInstance(page);
  await stubJson(page, '/api/me', meDto());
  await mount(<AppRoutes />, { hooksConfig: { route: `/mobile/sign-in?${query}` } });
}

async function look(page: Page, theme: 'light' | 'dark', size: { width: number; height: number }) {
  await page.setViewportSize(size);
  await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
}

const phone = { width: 390, height: 844 };
const desktop = { width: 1440, height: 900 };

for (const theme of ['light', 'dark'] as const) {
  test(`confirm at 390x844 ${theme}`, async ({ mount, page }) => {
    await look(page, theme, phone);
    await open(mount, page);
    await expect(page.getByTestId(ids.signIn)).toBeVisible();
    await expect(page).toHaveScreenshot(`confirm-390x844.${theme}.png`, { animations: 'disabled' });
  });

  test(`confirm at 1440x900 ${theme}`, async ({ mount, page }) => {
    await look(page, theme, desktop);
    await open(mount, page);
    await expect(page.getByTestId(ids.signIn)).toBeVisible();
    await expect(page).toHaveScreenshot(`confirm-1440x900.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`long device name ${theme}`, async ({ mount, page }) => {
    await look(page, theme, phone);
    await open(mount, page, queryFor(longDevice));
    await expect(page.getByTestId(ids.heading)).toContainText(longDevice);
    await expect(page).toHaveScreenshot(`long-device-name.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`couldn't reach ShipLean ${theme}`, async ({ mount, page }) => {
    await look(page, theme, phone);
    await open(mount, page);
    await page.route('**/api/auth/device-sessions/codes', (route) => route.abort());
    await page.getByTestId(ids.signIn).click();
    await expect(page.getByTestId(ids.failure)).toHaveText(
      "Couldn't reach ShipLean. Check your connection and try again.",
    );
    await expect(page).toHaveScreenshot(`couldnt-reach-shiplean.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`missing parameters ${theme}`, async ({ mount, page }) => {
    await look(page, theme, phone);
    await open(mount, page, queryFor(deviceName, { challenge: 'short' }));
    await expect(page.getByTestId(ids.missing)).toContainText(
      'This page opens from the ShipLean app. Start signing in there to get here.',
    );
    await expect(page.getByTestId(ids.signIn)).toHaveCount(0);
    await expect(page.getByTestId(ids.cancel)).toHaveCount(0);
    await expect(page).toHaveScreenshot(`missing-parameters.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`identity-proxy instance ${theme}`, async ({ mount, page }) => {
    await look(page, theme, phone);
    await stubProxyInstance(page);
    await stubJson(page, '/api/me', meDto());
    await mount(<AppRoutes />, { hooksConfig: { route: `/mobile/sign-in?${validQuery}` } });
    await expect(page.getByTestId(ids.proxyMessage)).toContainText("can't sign in to");
    await expect(page.getByTestId(ids.signIn)).toHaveCount(0);
    await expect(page.getByTestId(ids.backToApp)).toHaveText('Back to the app');
    await expect(page).toHaveScreenshot(`identity-proxy-instance.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`signed in on device after Sign in ${theme}`, async ({ mount, page }) => {
    await look(page, theme, phone);
    await open(mount, page);
    await stubCodes(page);
    await page.getByTestId(ids.signIn).click();
    await expect(page.getByTestId(ids.done)).toContainText(`Signed in on ${deviceName}`);
    await expect(page).toHaveScreenshot(`signed-in-on-device.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`nothing signed in after Cancel ${theme}`, async ({ mount, page }) => {
    await look(page, theme, phone);
    await open(mount, page);
    await page.getByTestId(ids.cancel).click();
    await expect(page.getByTestId(ids.cancelled)).toContainText('Nothing was signed in');
    await expect(page).toHaveScreenshot(`nothing-signed-in.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

test('Sign in posts the challenge and device name', async ({ mount, page }) => {
  await open(mount, page);
  let body: unknown = null;
  await page.route('**/api/auth/device-sessions/codes', (route) => {
    body = route.request().postDataJSON();
    return route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify({ code: 'code-123' }),
    });
  });
  await page.getByTestId(ids.signIn).click();
  await expect(page.getByTestId(ids.done)).toBeVisible();
  expect(body).toEqual({ challenge, deviceName });
});

test('after Sign in, Open the app links to shiplean://signed-in with the code, state and iss', async ({
  mount,
  page,
}) => {
  await open(mount, page);
  await stubCodes(page, 'abc 123');
  await page.getByTestId(ids.signIn).click();
  const origin = await page.evaluate(() => window.location.origin);
  await expect(page.getByTestId(ids.openApp)).toHaveAttribute(
    'href',
    `shiplean://signed-in?code=abc%20123&state=${stateValue}&iss=${encodeURIComponent(origin)}`,
  );
});

test('buttons are off while posting', async ({ mount, page }) => {
  await open(mount, page);
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/auth/device-sessions/codes', async (route) => {
    await gate;
    await route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify({ code: 'code-123' }),
    });
  });
  await page.getByTestId(ids.signIn).click();
  await expect(page.getByTestId(ids.signIn)).toBeDisabled();
  await expect(page.getByTestId(ids.signIn)).toHaveText('Signing in…');
  await expect(page.getByTestId(ids.cancel)).toBeDisabled();
  release();
  await expect(page.getByTestId(ids.done)).toBeVisible();
});

test('nothing is posted on load', async ({ mount, page }) => {
  const posts: string[] = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().includes('/device-sessions/'))
      posts.push(request.url());
  });
  await open(mount, page);
  await expect(page.getByTestId(ids.signIn)).toBeVisible();
  await expect(page.getByTestId(ids.does)).toBeVisible();
  expect(posts).toEqual([]);
});

test('a device name with markup renders as text', async ({ mount, page }) => {
  const markup = '<img src=x onerror=alert(1)><b>Pixel</b>';
  await open(mount, page, queryFor(markup));
  await expect(page.getByTestId(ids.device)).toContainText(markup);
  await expect(page.getByTestId(ids.heading)).toContainText(markup);
});
