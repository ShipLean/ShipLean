import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright';
import type { MeDto } from '@shiplean/core/api/me';
import type { ProjectSummaryDto } from '@shiplean/core/api/projects';
import { touchTestIds } from '../../../touch/testIds';
import { settingsTestIds as ids } from '../testIds';
import { ProfilePanelsHarness } from './ct/ProfilePanelsHarness';

// Your profile's panels on a phone (390×844, SHIP-733 L733a). Screenshots are compared only in CI; the assertions
// around them are what fail locally.
// Regenerate with scripts/update-page-shots.sh src/features/settings/profile/Profile.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;

const me: MeDto = {
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
};

const projects: ProjectSummaryDto[] = [
  { id: 'id-SHIP', key: 'SHIP', name: 'SHIP project', role: 'admin', memberCount: 3 },
];

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; url(): string; postDataJSON(): unknown };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const stub = async (page: Routable): Promise<string[]> => {
  const calls: string[] = [];
  let current = me;
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const method = request.method();
    const path = new URL(request.url()).pathname;
    if (method === 'GET') return route.fulfill({ json: path === '/api/me' ? current : projects });
    calls.push(`${method} ${path}`);
    if (path === '/api/me/profile') {
      const body = request.postDataJSON() as { name: string; avatarColour: string };
      current = { ...current, name: body.name.trim(), avatarColour: body.avatarColour };
    }
    if (path === '/api/auth/password') return route.fulfill({ json: current });
    if (path.startsWith('/api/me/preferences/')) {
      const key = path.split('/').pop() as 'theme' | 'density';
      current = { ...current, [key]: request.postDataJSON() as string };
    }
    return route.fulfill({ json: current });
  });
  return calls;
};

const box = async (locator: Locator) => {
  const b = await locator.boundingBox();
  if (!b) throw new Error('the element should render');
  return b;
};

for (const theme of THEMES) {
  test(`ProfilePhone_WhenOpened_ShouldMatchItsScreenshot_profile-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    const view = await mount(<ProfilePanelsHarness />);
    await expect(view.getByTestId(ids.profileName)).toHaveValue('Priya Shah');

    const name = await box(view.getByTestId(ids.profileName));
    expect(name.height).toBe(36);
    expect(name.width).toBeGreaterThan(300);
    await expect(view.getByTestId(ids.profileName)).toHaveCSS('font-size', '16px');
    expect((await box(view.getByTestId(ids.profileAvatar))).width).toBe(56);
    expect((await box(view.getByTestId(ids.profileColour).first())).width).toBe(32);
    const first = await box(view.getByTestId(ids.profileColour).nth(0));
    const sixth = await box(view.getByTestId(ids.profileColour).nth(5));
    expect(sixth.width).toBe(first.width);
    await expect(view.getByTestId(ids.profileSave)).toBeHidden();
    await expect(view.getByTestId(touchTestIds.saveBar)).toHaveCount(0);

    await expect(page).toHaveScreenshot(`profile-phone.${theme}.png`, { animations: 'disabled' });
  });

  test(`ProfilePhone_WhenEdited_ShouldMatchItsScreenshot_profile-dirty_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    const view = await mount(<ProfilePanelsHarness />);
    await view.getByTestId(ids.profileName).fill('Ada Lovelace');

    const bar = page.getByTestId(touchTestIds.saveBar);
    await expect(bar.getByTestId(touchTestIds.saveBarMessage)).toHaveText(
      'Unsaved profile changes',
    );
    await expect(view.getByTestId(ids.profileDirty)).toBeHidden();
    await expect(page).toHaveScreenshot(`profile-dirty.${theme}.png`, { animations: 'disabled' });
  });

  test(`ProfilePhone_WhenPasswordOpened_ShouldMatchItsScreenshot_profile-password-open_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    const view = await mount(<ProfilePanelsHarness />);
    await expect(view.getByTestId(ids.passwordForm)).toHaveCount(0);
    await view.getByTestId(ids.passwordOpen).tap();
    await view.getByTestId(ids.passwordCurrent).fill('old-password-1');

    const panel = view.getByTestId(ids.passwordPanel);
    await expect(panel).toHaveScreenshot(`profile-password-open.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`ProfilePhone_WhenOpened_ShouldMatchItsScreenshot_profile-appearance_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    const view = await mount(<ProfilePanelsHarness />);
    const panel = view.getByTestId(ids.appearancePanel);
    await expect(panel).toBeVisible();

    const options = panel.getByTestId(ids.themeOption);
    const widths = await Promise.all([0, 1, 2].map(async (i) => (await box(options.nth(i))).width));
    expect(Math.max(...widths) - Math.min(...widths)).toBeLessThanOrEqual(1);
    expect((await box(options.first())).height).toBe(36);
    await expect(panel).toHaveScreenshot(`profile-appearance.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

test('ProfilePhone_WhenNameCleared_ShouldAskForANameWithSaveOff', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfilePanelsHarness />);
  await view.getByTestId(ids.profileName).fill('');
  const bar = page.getByTestId(touchTestIds.saveBar);
  await expect(bar.getByTestId(touchTestIds.saveBarMessage)).toHaveText('Enter a name to save');
  await expect(bar.getByTestId(touchTestIds.saveBarSave)).toBeDisabled();
});

test('ProfilePhone_WhenSavedFromTheBar_ShouldPutAndClose', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<ProfilePanelsHarness />);
  await view.getByTestId(ids.profileName).fill('Ada Lovelace');
  await page.getByTestId(touchTestIds.saveBarSave).tap();
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
  expect(calls).toEqual(['PUT /api/me/profile']);
});

test('PasswordPhone_WhenCancelled_ShouldClearTheFieldsAndClose', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<ProfilePanelsHarness />);
  await view.getByTestId(ids.passwordOpen).tap();
  await view.getByTestId(ids.passwordCurrent).fill('old-password-1');
  await view.getByTestId(ids.passwordCancel).tap();
  await expect(view.getByTestId(ids.passwordForm)).toHaveCount(0);
  await view.getByTestId(ids.passwordOpen).tap();
  await expect(view.getByTestId(ids.passwordCurrent)).toHaveValue('');
});

test('PasswordPhone_WhenChanged_ShouldCloseTheFormAndToast', async ({ mount, page }) => {
  const calls = await stub(page);
  const view = await mount(<ProfilePanelsHarness />);
  await view.getByTestId(ids.passwordOpen).tap();
  await view.getByTestId(ids.passwordCurrent).fill('old-password-1');
  await view.getByTestId(ids.passwordNew).fill('a-new-password');
  await view.getByTestId(ids.passwordConfirm).fill('a-new-password');
  await view.getByTestId(ids.passwordSubmit).tap();
  await expect(view.getByTestId(ids.passwordForm)).toHaveCount(0);
  await expect(view.getByTestId(ids.passwordOpen)).toBeVisible();
  expect(calls).toEqual(['POST /api/auth/password']);
});
