import { expect, test } from '@playwright/experimental-ct-react';
import type { MeDto, TimeZoneChoicesDto } from '@shiplean/core/api/me';
import { settingsTestIds as ids } from '../../testIds';
import { TimeZonePanel } from './TimeZonePanel';

// The browser runs in Bangkok, so "this device's zone" is Asia/Bangkok.
test.use({ timezoneId: 'Asia/Bangkok' });

const NOT_SET = 'Not set: times show in UTC';

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
  timeZone: null,
  timeZoneLabel: NOT_SET,
};

const choices: TimeZoneChoicesDto = {
  choices: [
    { id: 'Europe/London', label: 'Europe/London (UTC+00:00)' },
    { id: 'Asia/Bangkok', label: 'Asia/Bangkok (UTC+07:00)' },
  ],
};

const labelOf = (zone: string) => choices.choices.find((c) => c.id === zone)?.label ?? zone;

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
  options: { zone?: string; refuse?: string; choicesFail?: boolean } = {},
) => {
  const sent: unknown[] = [];
  let current: MeDto = options.zone
    ? { ...me, timeZone: options.zone, timeZoneLabel: labelOf(options.zone) }
    : me;
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === '/api/me/time-zones') {
      return options.choicesFail
        ? route.fulfill({ status: 500, json: { code: 'internal', error: 'Something broke.' } })
        : route.fulfill({ json: choices });
    }
    if (path === '/api/me/time-zone') {
      const body = request.postDataJSON() as { timeZone: string; source: string };
      sent.push(body);
      if (options.refuse) {
        return route.fulfill({ status: 400, json: { code: 'validation', error: options.refuse } });
      }
      current = { ...current, timeZone: body.timeZone, timeZoneLabel: labelOf(body.timeZone) };
      return route.fulfill({ json: current });
    }
    return route.fulfill({ json: current });
  });
  return sent;
};

test('TimeZone_WhenNotSet_ShouldShowTheHintAndOfferTheDevicesZone', async ({ mount, page }) => {
  const sent = await stub(page);
  const view = await mount(<TimeZonePanel />);

  await expect(view.getByTestId(ids.timeZoneCurrent)).toHaveText(NOT_SET);
  const device = view.getByTestId(ids.timeZoneUseDevice);
  await expect(device).toHaveText("Use this device's zone (Asia/Bangkok)");
  await device.click();

  await expect(view.getByTestId(ids.timeZoneCurrent)).toHaveText('Asia/Bangkok (UTC+07:00)');
  await expect(device).toHaveCount(0);
  expect(sent).toEqual([{ timeZone: 'Asia/Bangkok', source: 'profile' }]);
});

test('TimeZone_WhenAnotherZoneIsPicked_ShouldSaveItAsProfileAndShowItsLabel', async ({
  mount,
  page,
}) => {
  const sent = await stub(page, { zone: 'Asia/Bangkok' });
  const view = await mount(<TimeZonePanel />);
  const select = view.getByTestId(ids.timeZoneSelect);
  await expect(select).toBeEnabled();

  await select.selectOption('Europe/London');

  await expect(view.getByTestId(ids.timeZoneCurrent)).toHaveText('Europe/London (UTC+00:00)');
  await expect(select).toHaveValue('Europe/London');
  expect(sent).toEqual([{ timeZone: 'Europe/London', source: 'profile' }]);
});

test('TimeZone_WhenSetToTheDevicesZone_ShouldHideTheDeviceButton', async ({ mount, page }) => {
  await stub(page, { zone: 'Asia/Bangkok' });
  const view = await mount(<TimeZonePanel />);

  await expect(view.getByTestId(ids.timeZoneCurrent)).toHaveText('Asia/Bangkok (UTC+07:00)');
  await expect(view.getByTestId(ids.timeZoneSelect)).toHaveValue('Asia/Bangkok');
  await expect(view.getByTestId(ids.timeZoneUseDevice)).toHaveCount(0);
});

test('TimeZone_WhenServerRefuses_ShouldShowItsMessageAndKeepTheZone', async ({ mount, page }) => {
  await stub(page, { zone: 'Asia/Bangkok', refuse: 'That is not a time zone ShipLean knows.' });
  const view = await mount(<TimeZonePanel />);
  const select = view.getByTestId(ids.timeZoneSelect);
  await expect(select).toBeEnabled();

  await select.selectOption('Europe/London');

  await expect(view.getByTestId(ids.timeZoneError)).toHaveText(
    'That is not a time zone ShipLean knows.',
  );
  await expect(view.getByTestId(ids.timeZoneCurrent)).toHaveText('Asia/Bangkok (UTC+07:00)');
});

// Mutation: put `disabled={choices.data === undefined || saving}` back on the select (a disabled control drops keyboard focus).
test('TimeZone_WhenAKeyboardSaveIsRefused_ShouldKeepFocusOnTheSelectAndAnnounceTheError', async ({
  mount,
  page,
}) => {
  await stub(page, { zone: 'Asia/Bangkok', refuse: 'That is not a time zone ShipLean knows.' });
  const view = await mount(<TimeZonePanel />);
  const select = view.getByTestId(ids.timeZoneSelect);
  await expect(select).toBeEnabled();

  await select.focus();
  await page.keyboard.press('ArrowUp');

  const error = view.getByTestId(ids.timeZoneError);
  await expect(error).toHaveText('That is not a time zone ShipLean knows.');
  await expect(error).toHaveAttribute('role', 'alert');
  await expect(select).toBeFocused();
});

test('TimeZone_WhenChoicesFailToLoad_ShouldShowTheErrorNotAnEmptySelect', async ({
  mount,
  page,
}) => {
  await stub(page, { choicesFail: true });
  const view = await mount(<TimeZonePanel />);

  await expect(view.getByTestId(ids.timeZoneChoicesError)).toHaveText(
    'The list of time zones could not be loaded. Reload the page to try again.',
  );
  await expect(view.getByTestId(ids.timeZoneSelect)).toHaveCount(0);
  await expect(view.getByTestId(ids.timeZoneCurrent)).toHaveText(NOT_SET);
});

test('TimeZone_WhenOnAPhone_ShouldFitTheWidthWithTouchSizedControls', async ({ mount, page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await stub(page);
  const view = await mount(<TimeZonePanel />);
  const select = view.getByTestId(ids.timeZoneSelect);
  await expect(select).toBeEnabled();

  const panel = await view.boundingBox();
  const field = await select.boundingBox();
  const device = await view.getByTestId(ids.timeZoneUseDevice).boundingBox();
  expect(panel?.width ?? Infinity).toBeLessThanOrEqual(390);
  expect((field?.x ?? Infinity) + (field?.width ?? 0)).toBeLessThanOrEqual(390);
  expect(field?.height ?? 0).toBeGreaterThanOrEqual(32);
  expect(device?.height ?? 0).toBeGreaterThanOrEqual(32);
});
