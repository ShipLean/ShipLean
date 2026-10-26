import { expect, test } from '@playwright/experimental-ct-react';
import type { MeDto } from '../../../../api/me';
import { ToastHost } from '../../../../components/ToastHost';
import { componentTestIds } from '../../../../components/testIds';
import { settingsTestIds as ids } from '../../testIds';
import { ProfileIdentity } from './ProfileIdentity';

const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
);

const meOf = (over: Partial<MeDto> = {}): MeDto => ({
  id: 'me',
  name: 'Priya Shah',
  email: 'priya@example.test',
  initials: 'PS',
  avatarColour: '#2f7fd8',
  photoUrl: null,
  isInstanceAdmin: true,
  status: 'active',
  hasProjects: true,
  canCreateProjects: true,
  theme: 'os',
  density: 'compact',
  ...over,
});

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; url(): string; postDataJSON(): unknown };
      fallback(): Promise<void>;
      fulfill(response: {
        json?: unknown;
        status?: number;
        body?: Buffer;
        contentType?: string;
      }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

interface Stubbed {
  calls: string[];
  me: MeDto;
  gets: { count: number };
  refuse: { message: string | null };
}

const stub = async (page: Routable, start: MeDto = meOf()): Promise<Stubbed> => {
  const stubbed: Stubbed = { calls: [], me: start, gets: { count: 0 }, refuse: { message: null } };
  let version = 0;
  await page.route('**/api/users/**', (route) =>
    route.fulfill({ body: PNG, contentType: 'image/png' }),
  );
  await page.route('**/api/me**', async (route) => {
    const request = route.request();
    const method = request.method();
    const path = new URL(request.url()).pathname;
    if (method === 'GET') {
      stubbed.gets.count += 1;
      return route.fulfill({ json: stubbed.me });
    }
    stubbed.calls.push(`${method} ${path}`);
    if (stubbed.refuse.message !== null) {
      return route.fulfill({
        status: 400,
        json: { code: 'validation', error: stubbed.refuse.message },
      });
    }
    if (path === '/api/me/profile') {
      const body = request.postDataJSON() as { name: string; avatarColour: string };
      const name = body.name.trim();
      stubbed.me = { ...stubbed.me, name, avatarColour: body.avatarColour };
    } else if (method === 'PUT') {
      version += 1;
      stubbed.me = { ...stubbed.me, photoUrl: `/api/users/me/photo?v=${version}` };
    } else {
      stubbed.me = { ...stubbed.me, photoUrl: null };
    }
    return route.fulfill({ json: stubbed.me });
  });
  return stubbed;
};

test('Identity_WhenLoaded_ShouldShowNameColourAndReadOnlyEmail', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfileIdentity />);

  await expect(view.getByTestId(ids.profileName)).toHaveValue('Priya Shah');
  await expect(view.getByTestId(ids.profileEmail)).toHaveText('priya@example.test');
  await expect(view.getByTestId(ids.profileAvatar)).toHaveText('PS');
  await expect(view.getByTestId(ids.profileColour)).toHaveCount(10);
  await expect(
    view.locator(`[data-testid="${ids.profileColour}"][aria-pressed="true"]`),
  ).toHaveAttribute('data-colour', '#2f7fd8');
  await expect(view.getByTestId(ids.profileSave)).toBeDisabled();
  await expect(view.getByTestId(ids.profilePhotoRemove)).toHaveCount(0);
});

test('Identity_WhenNameAndColourEdited_ShouldPreviewThenSaveAndRefetchMe', async ({
  mount,
  page,
}) => {
  const stubbed = await stub(page);
  const view = await mount(<ProfileIdentity />);
  await expect(view.getByTestId(ids.profileName)).toHaveValue('Priya Shah');
  const getsBefore = stubbed.gets.count;

  await view.getByTestId(ids.profileName).fill('Ada Lovelace');
  await view.locator(`[data-colour="#7b4fc9"]`).click();

  await expect(view.getByTestId(ids.profileAvatar)).toHaveText('AL');
  await expect(view.getByTestId(ids.profileHeading)).toHaveText('Ada Lovelace');
  await expect(view.getByTestId(ids.profilePreviewCard)).toContainText('AL');
  await expect(view.getByTestId(ids.profilePreviewPresence)).toContainText('AL');
  await expect(view.getByTestId(ids.profileDirty)).toBeVisible();

  await view.getByTestId(ids.profileSave).click();

  await expect(view.getByTestId(ids.profileDirty)).toHaveCount(0);
  expect(stubbed.calls).toEqual(['PUT /api/me/profile']);
  expect(stubbed.me.avatarColour).toBe('#7b4fc9');
  expect(stubbed.gets.count).toBeGreaterThan(getsBefore);
  await expect(view.getByTestId(ids.profileSave)).toBeDisabled();
});

test('Identity_WhenNameBlank_ShouldBlockSave', async ({ mount, page }) => {
  const stubbed = await stub(page);
  const view = await mount(<ProfileIdentity />);

  await view.getByTestId(ids.profileName).fill('   ');

  await expect(view.getByTestId(ids.profileNameError)).toHaveText('Enter a name');
  await expect(view.getByTestId(ids.profileSave)).toBeDisabled();
  expect(stubbed.calls).toEqual([]);
});

test('Identity_WhenNameHasControlCharacter_ShouldBlockSave', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfileIdentity />);

  await view.getByTestId(ids.profileName).fill('Ada\u0007Lovelace');

  await expect(view.getByTestId(ids.profileNameError)).toContainText('control characters');
  await expect(view.getByTestId(ids.profileSave)).toBeDisabled();
});

test('Identity_WhenPhotoPicked_ShouldPreviewThenUploadOnSave', async ({ mount, page }) => {
  const stubbed = await stub(page);
  const view = await mount(<ProfileIdentity />);
  await expect(view.getByTestId(ids.profilePhotoUpload)).toHaveText('Upload photo');

  await view
    .getByTestId(ids.profilePhotoInput)
    .setInputFiles({ name: 'me.png', mimeType: 'image/png', buffer: PNG });

  await expect(view.getByTestId(ids.profileAvatar)).toHaveAttribute('data-has-photo', 'true');
  await expect(view.getByTestId(ids.profilePhotoUpload)).toHaveText('Replace photo');
  await expect(
    view.getByTestId(ids.profilePreviewCard).getByTestId(componentTestIds.avatarPhoto),
  ).toBeVisible();
  expect(stubbed.calls).toEqual([]);

  await view.getByTestId(ids.profileSave).click();

  await expect(view.getByTestId(ids.profileDirty)).toHaveCount(0);
  expect(stubbed.calls).toEqual(['PUT /api/me/photo']);
  await expect(view.getByTestId(ids.profileAvatar).locator('img')).toHaveAttribute(
    'src',
    '/api/users/me/photo?v=1',
  );
});

test('Identity_WhenPhotoOverTwoMegabytes_ShouldRefuseBeforeUploading', async ({ mount, page }) => {
  const stubbed = await stub(page);
  const view = await mount(<ProfileIdentity />);

  await view.getByTestId(ids.profilePhotoInput).setInputFiles({
    name: 'big.png',
    mimeType: 'image/png',
    buffer: Buffer.alloc(2 * 1024 * 1024 + 1),
  });

  await expect(view.getByTestId(ids.profilePhotoError)).toContainText('over 2 MB');
  await expect(view.getByTestId(ids.profileAvatar)).toHaveAttribute('data-has-photo', 'false');
  await expect(view.getByTestId(ids.profileSave)).toBeDisabled();
  expect(stubbed.calls).toEqual([]);
});

test('Identity_WhenFileIsNotAnImage_ShouldRefuseBeforeUploading', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfileIdentity />);

  await view.getByTestId(ids.profilePhotoInput).setInputFiles({
    name: 'notes.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('hello'),
  });

  await expect(view.getByTestId(ids.profilePhotoError)).toContainText('PNG, JPEG or WebP');
  await expect(view.getByTestId(ids.profileSave)).toBeDisabled();
});

test('Identity_WhenSavedPhotoRemoved_ShouldDeleteOnSave', async ({ mount, page }) => {
  const stubbed = await stub(page, meOf({ photoUrl: '/api/users/me/photo?v=3' }));
  const view = await mount(<ProfileIdentity />);
  await expect(view.getByTestId(ids.profileAvatar)).toHaveAttribute('data-has-photo', 'true');

  await view.getByTestId(ids.profilePhotoRemove).click();

  await expect(view.getByTestId(ids.profileAvatar)).toHaveAttribute('data-has-photo', 'false');
  await expect(view.getByTestId(ids.profileAvatar)).toHaveText('PS');
  expect(stubbed.calls).toEqual([]);

  await view.getByTestId(ids.profileSave).click();

  await expect(view.getByTestId(ids.profileDirty)).toHaveCount(0);
  expect(stubbed.calls).toEqual(['DELETE /api/me/photo']);
  await expect(view.getByTestId(ids.profilePhotoUpload)).toHaveText('Upload photo');
});

test('Identity_WhenServerRefuses_ShouldShowItAndKeepTheEdits', async ({ mount, page }) => {
  const stubbed = await stub(page);
  stubbed.refuse.message = 'The name can have at most 100 characters.';
  const view = await mount(<ProfileIdentity />);

  await view.getByTestId(ids.profileName).fill('Ada Lovelace');
  await view.getByTestId(ids.profileSave).click();

  await expect(view.getByTestId(ids.profileSaveError)).toHaveText(
    'The name can have at most 100 characters.',
  );
  await expect(view.getByTestId(ids.profileName)).toHaveValue('Ada Lovelace');
  await expect(view.getByTestId(ids.profileDirty)).toBeVisible();
});

test('Identity_WhenDiscarded_ShouldGoBackToTheSavedProfile', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfileIdentity />);

  await view.getByTestId(ids.profileName).fill('Ada Lovelace');
  await view.locator(`[data-colour="#7b4fc9"]`).click();
  await view.getByTestId(ids.profileDiscard).click();

  await expect(view.getByTestId(ids.profileName)).toHaveValue('Priya Shah');
  await expect(view.getByTestId(ids.profileAvatar)).toHaveText('PS');
  await expect(view.getByTestId(ids.profileDirty)).toHaveCount(0);
});

test('Identity_WhenOnlyColourChanged_ShouldSaveIt', async ({ mount, page }) => {
  const stubbed = await stub(page);
  const view = await mount(<ProfileIdentity />);
  await expect(view.getByTestId(ids.profileName)).toHaveValue('Priya Shah');

  await view.locator(`[data-colour="#7b4fc9"]`).click();

  await expect(view.getByTestId(ids.profileSave)).toBeEnabled();
  await view.getByTestId(ids.profileSave).click();

  await expect(view.getByTestId(ids.profileDirty)).toHaveCount(0);
  expect(stubbed.calls).toEqual(['PUT /api/me/profile']);
  expect(stubbed.me.avatarColour).toBe('#7b4fc9');
});

test('Identity_WhenSaved_ShouldToastThatEveryoneSeesIt', async ({ mount, page }) => {
  await stub(page);
  await mount(
    <>
      <ToastHost />
      <ProfileIdentity />
    </>,
  );
  await page.getByTestId(ids.profileName).fill('Ada Lovelace');

  await page.getByTestId(ids.profileSave).click();

  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'Profile saved. Everyone sees the change straight away',
  );
});

const proxyInstance = async (page: Routable) => {
  await page.route('**/api/auth/instance', (route) =>
    route.fulfill({
      json: { name: 'ShipLean', signIn: 'proxy', signOutUrl: '/oauth2/sign_out' },
    }),
  );
};

test('Identity_WhenProxyMode_ShouldShowTheNameReadOnlyWithHelpText', async ({ mount, page }) => {
  await stub(page);
  await proxyInstance(page);
  const view = await mount(<ProfileIdentity />);

  await expect(view.getByTestId(ids.profileNameReadOnly)).toHaveText('Priya Shah');
  await expect(view.getByTestId(ids.profileName)).toHaveCount(0);
  await expect(view).toContainText('Your name comes from your sign-in provider.');
});

test('Identity_WhenProxyMode_ShouldSayTheEmailComesFromTheProvider', async ({ mount, page }) => {
  await stub(page);
  await proxyInstance(page);
  const view = await mount(<ProfileIdentity />);

  await expect(view.getByTestId(ids.profileEmail)).toHaveText('priya@example.test');
  await expect(view.getByTestId(ids.profileEmailSource)).toHaveText(
    'Your email comes from your sign-in provider.',
  );
});

test('Identity_WhenProxyModeColourChanged_ShouldSendTheStoredName', async ({ mount, page }) => {
  const stubbed = await stub(page);
  await proxyInstance(page);
  const bodies: unknown[] = [];
  await page.route('**/api/me/profile', (route) => {
    bodies.push(route.request().postDataJSON());
    return route.fallback();
  });
  const view = await mount(<ProfileIdentity />);
  await expect(view.getByTestId(ids.profileNameReadOnly)).toBeVisible();

  await view.locator(`[data-colour="#7b4fc9"]`).click();
  await view.getByTestId(ids.profileSave).click();

  await expect(view.getByTestId(ids.profileDirty)).toHaveCount(0);
  expect(bodies).toEqual([{ name: 'Priya Shah', avatarColour: '#7b4fc9' }]);
  expect(stubbed.me.avatarColour).toBe('#7b4fc9');
});

test('Identity_WhenProxyModeSaveForbidden_ShouldShowTheServerMessage', async ({ mount, page }) => {
  await stub(page);
  await proxyInstance(page);
  await page.route('**/api/me/profile', (route) =>
    route.fulfill({
      status: 403,
      json: { code: 'forbidden', error: 'Your name comes from your sign-in provider.' },
    }),
  );
  const view = await mount(<ProfileIdentity />);
  await expect(view.getByTestId(ids.profileNameReadOnly)).toBeVisible();

  await view.locator(`[data-colour="#7b4fc9"]`).click();
  await view.getByTestId(ids.profileSave).click();

  await expect(view.getByTestId(ids.profileSaveError)).toHaveText(
    'Your name comes from your sign-in provider.',
  );
  await expect(view.getByTestId(ids.profileDirty)).toBeVisible();
});

test('Identity_WhenPasswordMode_ShouldKeepTheNameInputAndEmailText', async ({ mount, page }) => {
  await stub(page);
  await page.route('**/api/auth/instance', (route) =>
    route.fulfill({ json: { name: 'ShipLean', signIn: 'password', signOutUrl: null } }),
  );
  const view = await mount(<ProfileIdentity />);

  await expect(view.getByTestId(ids.profileName)).toHaveValue('Priya Shah');
  await expect(view.getByTestId(ids.profileNameReadOnly)).toHaveCount(0);
  await expect(view.getByTestId(ids.profileEmailSource)).toHaveCount(0);
  await expect(view).toContainText('Only an instance admin can change it.');
});

test('Identity_WhenProxyModeNameRenamedUpstream_ShouldSendTheNewNameAndStayClean', async ({
  mount,
  page,
}) => {
  const stubbed = await stub(page);
  await proxyInstance(page);
  const bodies: unknown[] = [];
  await page.route('**/api/me/profile', (route) => {
    bodies.push(route.request().postDataJSON());
    return route.fallback();
  });
  const view = await mount(<ProfileIdentity />);
  await expect(view.getByTestId(ids.profileNameReadOnly)).toHaveText('Priya Shah');

  stubbed.me = { ...stubbed.me, name: 'Priya Shah-Lee', initials: 'PS' };
  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));
  await expect(view.getByTestId(ids.profileNameReadOnly)).toHaveText('Priya Shah-Lee');
  await expect(view.getByTestId(ids.profileDirty)).toHaveCount(0);

  await view.locator(`[data-colour="#7b4fc9"]`).click();
  await view.getByTestId(ids.profileSave).click();

  await expect(view.getByTestId(ids.profileDirty)).toHaveCount(0);
  expect(bodies).toEqual([{ name: 'Priya Shah-Lee', avatarColour: '#7b4fc9' }]);
});
