import { expect, test } from '@playwright/experimental-ct-react';
import type { InstanceSettingsDto } from '../../../api/projects';
import { settingsTestIds as ids } from '../testIds';
import { InstanceGeneral } from './InstanceGeneral';

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; postDataJSON(): unknown };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const saved: InstanceSettingsDto = {
  name: 'Larkspur Engineering',
  baseUrl: 'https://shiplean.larkspur.dev',
  projectCreation: 'everyone',
};

const stub = async (page: Routable, status = 200) => {
  const puts: unknown[] = [];
  let current = saved;
  await page.route('**/api/instance/settings', async (route) => {
    const request = route.request();
    if (status !== 200)
      return route.fulfill({
        status,
        json: { error: 'Nope.', code: status === 403 ? 'forbidden' : 'validation' },
      });
    if (request.method() === 'PUT') {
      const body = request.postDataJSON() as InstanceSettingsDto;
      puts.push(body);
      current = { ...body, baseUrl: body.baseUrl.replace(/\/$/, '') };
    }
    return route.fulfill({ json: current });
  });
  return puts;
};

test('General_WhenBaseUrlDiffersFromAddressInUse_ShouldWarnWithTheAddress', async ({
  mount,
  page,
}) => {
  await stub(page);
  const view = await mount(<InstanceGeneral />);

  const origin = await page.evaluate(() => window.location.origin);
  await expect(view.getByTestId(ids.instanceUrlMismatch)).toContainText(origin);
  await expect(view.getByTestId(ids.instanceUrlMatch)).toHaveCount(0);
});

test('General_WhenBaseUrlMatchesAddressInUse_ShouldConfirmIt', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceGeneral />);
  const origin = await page.evaluate(() => window.location.origin);

  await view.getByTestId(ids.instanceBaseUrl).fill(`${origin}/`);

  await expect(view.getByTestId(ids.instanceUrlMatch)).toBeVisible();
  await expect(view.getByTestId(ids.instanceUrlMismatch)).toHaveCount(0);
});

test('General_WhenBaseUrlIsNotHttps_ShouldWarnAboutCookies', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceGeneral />);
  await expect(view.getByTestId(ids.instanceUrlInsecure)).toHaveCount(0);

  await view.getByTestId(ids.instanceBaseUrl).fill('http://shiplean.larkspur.dev');

  await expect(view.getByTestId(ids.instanceUrlInsecure)).toBeVisible();
});

test('General_WhenEdited_ShouldSaveOneDocumentAndDiscardRestoresIt', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mount(<InstanceGeneral />);
  await expect(view.getByTestId(ids.instanceSave)).toBeDisabled();

  await view.getByTestId(ids.instanceName).fill('Renamed');
  await view.getByTestId(ids.instanceCreationAdmins).click();
  await expect(view.getByTestId(ids.instanceDirty)).toHaveText('Unsaved changes');
  await view.getByTestId(ids.instanceDiscard).click();
  await expect(view.getByTestId(ids.instanceName)).toHaveValue('Larkspur Engineering');
  await expect(view.getByTestId(ids.instanceCreationEveryone)).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  await view.getByTestId(ids.instanceName).fill('Renamed');
  await view.getByTestId(ids.instanceCreationAdmins).click();
  await view.getByTestId(ids.instanceSave).click();

  await expect(view.getByTestId(ids.instanceDirty)).toHaveText('Saved');
  expect(puts).toEqual([{ ...saved, name: 'Renamed', projectCreation: 'admins' }]);
});

test('General_WhenEditedBackToSaved_ShouldNotBeDirty', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceGeneral />);

  await view.getByTestId(ids.instanceName).fill('Other');
  await view.getByTestId(ids.instanceName).fill('Larkspur Engineering');

  await expect(view.getByTestId(ids.instanceDirty)).toHaveText('Saved');
});

test('General_WhenSaveRefused_ShouldShowTheServerMessage', async ({ mount, page }) => {
  await page.route('**/api/instance/settings', async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: saved });
    return route.fulfill({
      status: 400,
      json: { error: 'The base URL must be an http or https address.', code: 'validation' },
    });
  });
  const view = await mount(<InstanceGeneral />);

  await view.getByTestId(ids.instanceBaseUrl).fill('ftp://x');
  await view.getByTestId(ids.instanceSave).click();

  await expect(view.getByTestId(ids.instanceSaveError)).toContainText('base URL must be');
  await expect(view.getByTestId(ids.instanceDirty)).toHaveText('Unsaved changes');
});

test('General_WhenNotInstanceAdmin_ShouldShowDeniedNotTheForm', async ({ mount, page }) => {
  await stub(page, 403);
  const view = await mount(<InstanceGeneral />);

  await expect(page.getByTestId(ids.instanceDenied)).toBeVisible();
  await expect(view.getByTestId(ids.instanceName)).toHaveCount(0);
});

test('General_WhenLoaded_ShouldShowTheFixedSignInRules', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InstanceGeneral />);

  await expect(view.getByTestId(ids.instanceSignInFacts)).toContainText('10 wrong passwords');
});

// The server's document changes under the admin: the second and later reads return `later`.
const stubChangingOnRead = async (page: Routable, later: InstanceSettingsDto) => {
  const puts: InstanceSettingsDto[] = [];
  let reads = 0;
  let saved$ = later;
  await page.route('**/api/instance/settings', async (route) => {
    const request = route.request();
    if (request.method() === 'PUT') {
      saved$ = request.postDataJSON() as InstanceSettingsDto;
      puts.push(saved$);
      return route.fulfill({ json: saved$ });
    }
    reads += 1;
    return route.fulfill({ json: reads === 1 ? saved : later });
  });
  return puts;
};

test('General_WhenSettingsChangeWhileNameIsEdited_ShouldUpdateTheUntouchedFieldAndKeepTheEdit', async ({
  mount,
  page,
}) => {
  const puts = await stubChangingOnRead(page, { ...saved, projectCreation: 'admins' });
  const view = await mount(<InstanceGeneral />);
  await view.getByTestId(ids.instanceName).fill('Renamed');

  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));

  await expect(view.getByTestId(ids.instanceCreationAdmins)).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  await expect(view.getByTestId(ids.instanceName)).toHaveValue('Renamed');
  await view.getByTestId(ids.instanceSave).click();
  await expect(view.getByTestId(ids.instanceDirty)).toHaveText('Saved');
  await expect(view.getByTestId(ids.instanceName)).toHaveValue('Renamed');
  await expect(view.getByTestId(ids.instanceCreationAdmins)).toHaveAttribute(
    'aria-pressed',
    'true',
  );
  expect(puts).toEqual([{ ...saved, name: 'Renamed', projectCreation: 'admins' }]);
});

test('General_WhenSettingsChangedSinceLastRead_ShouldSendOnlyTheEditOverTheLatest', async ({
  mount,
  page,
}) => {
  const puts = await stubChangingOnRead(page, { ...saved, projectCreation: 'admins' });
  const view = await mount(<InstanceGeneral />);
  await view.getByTestId(ids.instanceName).fill('Renamed');

  await view.getByTestId(ids.instanceSave).click();

  await expect(view.getByTestId(ids.instanceDirty)).toHaveText('Saved');
  expect(puts).toEqual([{ ...saved, name: 'Renamed', projectCreation: 'admins' }]);
});

test('General_WhileSaving_ShouldLockTheFieldsSoNoTypingIsLost', async ({ mount, page }) => {
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/instance/settings', async (route) => {
    if (route.request().method() === 'PUT') {
      await gate;
      return route.fulfill({ json: { ...saved, name: 'Renamed' } });
    }
    return route.fulfill({ json: saved });
  });
  const view = await mount(<InstanceGeneral />);
  await view.getByTestId(ids.instanceName).fill('Renamed');

  await view.getByTestId(ids.instanceSave).click();

  await expect(view.getByTestId(ids.instanceName)).toBeDisabled();
  await expect(view.getByTestId(ids.instanceBaseUrl)).toBeDisabled();
  await expect(view.getByTestId(ids.instanceCreationAdmins)).toBeDisabled();
  release();
  await expect(view.getByTestId(ids.instanceName)).toBeEnabled();
  await expect(view.getByTestId(ids.instanceDirty)).toHaveText('Saved');
});
