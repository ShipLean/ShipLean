import { expect, test } from '@playwright/experimental-ct-react';
import type { DeviceSessionDto } from '@shiplean/core/api/deviceSessions';
import type { Page } from 'playwright';
import { ToastHost } from '../../components/ToastHost';
import { componentTestIds } from '../../components/testIds';
import { iphone, pixel, twoDevices } from './fixtures';
import { SignedInDevicesPanel } from './SignedInDevicesPanel';
import { devicesTestIds as ids } from './testIds';

const LIST = '**/api/auth/device-sessions';
const phone = { width: 390, height: 844 };
const desktop = { width: 1440, height: 900 };

const stubMcp = (page: Page) =>
  page.route('**/api/agents/mcp-url', (route) =>
    route.fulfill({ json: { mcpUrl: 'https://shiplean.larkspur.dev/mcp' } }),
  );

const stubList = (page: Page, list: DeviceSessionDto[]) =>
  page.route(LIST, (route) => route.fulfill({ json: list }));

const panel = (
  <div className="p-4">
    <SignedInDevicesPanel />
    <ToastHost />
  </div>
);

async function look(page: Page, theme: 'light' | 'dark', size: { width: number; height: number }) {
  await page.setViewportSize(size);
  await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
}

for (const theme of ['light', 'dark'] as const) {
  test(`two devices at 1440x900 ${theme}`, async ({ mount, page }) => {
    await look(page, theme, desktop);
    await stubList(page, twoDevices);
    await mount(panel);
    await expect(page.getByTestId(ids.row)).toHaveCount(2);
    await expect(page).toHaveScreenshot(`two-devices-1440x900.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`sign-out confirmation at 1440x900 ${theme}`, async ({ mount, page }) => {
    await look(page, theme, desktop);
    await stubList(page, twoDevices);
    await mount(panel);
    await page.getByTestId(ids.signOut).first().click();
    await expect(page.getByTestId(ids.confirm)).toBeVisible();
    await expect(page).toHaveScreenshot(`sign-out-confirmation-1440x900.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`sign-out confirmation at 390x844 ${theme}`, async ({ mount, page }) => {
    await look(page, theme, phone);
    await stubList(page, twoDevices);
    await mount(panel);
    await page.getByTestId(ids.signOut).first().click();
    await expect(page.getByTestId(ids.confirm)).toBeVisible();
    await expect(page).toHaveScreenshot(`sign-out-confirmation-390x844.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`no phones ${theme}`, async ({ mount, page }) => {
    await look(page, theme, desktop);
    await stubList(page, []);
    await mount(panel);
    await expect(page.getByTestId(ids.empty)).toHaveText('No phones are signed in.');
    await expect(page).toHaveScreenshot(`no-phones.${theme}.png`, { animations: 'disabled' });
  });

  test(`two devices at 390x844 ${theme}`, async ({ mount, page }) => {
    await look(page, theme, phone);
    await stubList(page, twoDevices);
    await mount(panel);
    await expect(page.getByTestId(ids.row)).toHaveCount(2);
    await expect(page).toHaveScreenshot(`two-devices-390x844.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`the list failing to load shows the panel's error state ${theme}`, async ({
    mount,
    page,
  }) => {
    await look(page, theme, desktop);
    await page.route(LIST, (route) =>
      route.fulfill({
        status: 500,
        json: { code: 'internal_error', error: 'Something went wrong.' },
      }),
    );
    await mount(panel);
    await expect(page.getByTestId(ids.failed)).toHaveText(
      "Couldn't load your signed-in devices. Reload the page to try again.",
    );
    await expect(page).toHaveScreenshot(`load-failed.${theme}.png`, { animations: 'disabled' });
  });
}

test('rows show the server labels, the prefix and the host', async ({ mount, page }) => {
  await stubList(page, twoDevices);
  await stubMcp(page);
  await mount(panel);
  const first = page.getByTestId(ids.row).first();
  await expect(first.getByTestId(ids.name)).toHaveText(iphone.name);
  await expect(first.getByTestId(ids.prefix)).toHaveText('shl_dev_k3Xq9TbA…');
  await expect(first.getByTestId(ids.when)).toHaveText('Last used 2h ago · Signed in 3 Nov');
  await expect(page.getByTestId(ids.addLine)).toHaveText(
    'To add a phone, sign in to shiplean.larkspur.dev from the ShipLean app.',
  );
});

test('Sign out asks first, then revokes and toasts', async ({ mount, page }) => {
  let current = twoDevices;
  await page.route(LIST, (route) => route.fulfill({ json: current }));
  const deleted: string[] = [];
  await page.route('**/api/auth/device-sessions/*', (route) => {
    deleted.push(`${route.request().method()} ${new URL(route.request().url()).pathname}`);
    current = current.filter((d) => !route.request().url().endsWith(d.id));
    return route.fulfill({ status: 204 });
  });
  await mount(panel);

  await page.getByTestId(ids.signOut).nth(1).click();
  await expect(page.getByTestId(ids.confirm)).toContainText(
    `Sign out ${pixel.name}? The app there goes back to Sign in straight away. Its server and email stay filled in.`,
  );
  expect(deleted).toEqual([]);

  await page.getByTestId(ids.confirmGo).click();

  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  await expect(page.getByTestId(ids.confirm)).toHaveCount(0);
  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    `${pixel.name} is signed out.`,
  );
  expect(deleted).toEqual([`DELETE /api/auth/device-sessions/${pixel.id}`]);
});

test('Cancel leaves the device listed', async ({ mount, page }) => {
  await stubList(page, twoDevices);
  let deletes = 0;
  await page.route('**/api/auth/device-sessions/*', (route) => {
    deletes += 1;
    return route.fulfill({ status: 204 });
  });
  await mount(panel);

  await page.getByTestId(ids.signOut).first().click();
  await page.getByTestId(ids.confirmCancel).click();

  await expect(page.getByTestId(ids.confirm)).toHaveCount(0);
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  expect(deletes).toBe(0);
});

test('a failed revoke shows the error, closes the confirm and reads the list again', async ({
  mount,
  page,
}) => {
  let reads = 0;
  await page.route(LIST, (route) => {
    reads += 1;
    return route.fulfill({ json: twoDevices });
  });
  await page.route('**/api/auth/device-sessions/*', (route) =>
    route.fulfill({
      status: 404,
      json: { code: 'not_found', error: 'That device is already signed out.' },
    }),
  );
  await mount(panel);

  await page.getByTestId(ids.signOut).first().click();
  await page.getByTestId(ids.confirmGo).click();

  await expect(page.getByTestId(ids.signOutError)).toHaveText('That device is already signed out.');
  await expect(page.getByTestId(ids.confirm)).toHaveCount(0);
  await expect.poll(() => reads).toBe(2);
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
});
