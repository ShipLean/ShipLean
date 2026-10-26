import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { realtimeTestIds } from '../realtime/testIds';
import { quickSearchTestIds } from '../quickSearch/testIds';
import { shellHarnessIds as harness } from './ct/harnessIds';
import { ShellHarness } from './ct/ShellHarness';
import { stubProxyInstance } from '../auth/authFixtures';
import { meDto, sprint, stubShell } from './shellFixtures';
import { shellTestIds as ids } from './testIds';

test('Picker_WhenSearchTyped_ShouldFilterByKeyOrName', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await page.getByTestId(ids.projectButton).click();
  await expect(page.getByTestId(ids.pickerSearch)).toBeFocused();
  await expect(page.getByTestId(ids.pickerOption)).toHaveCount(3);

  await page.getByTestId(ids.pickerSearch).fill('pay');
  await expect(page.getByTestId(ids.pickerOption)).toHaveCount(1);
  await expect(page.getByTestId(ids.pickerOption)).toHaveAttribute('data-key', 'PAY');
  await expect(page.getByTestId(ids.pickerMatch)).toHaveText(['PAY', 'Pay']);

  await page.getByTestId(ids.pickerSearch).fill('MOB');
  await expect(page.getByTestId(ids.pickerOption)).toHaveAttribute('data-key', 'MOB');

  await page.getByTestId(ids.pickerSearch).fill('nothing');
  await expect(page.getByTestId(ids.pickerNone)).toBeVisible();
});

test('Picker_WhenNoSearch_ShouldListRecentThenAllYourProjects', async ({ mount, page }) => {
  await stubShell(page, { recent: ['id-SHIP', 'id-MOB'] });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId(ids.projectButton)).toContainText('Shipping');
  await page.getByTestId(ids.projectButton).click();
  await expect(page.getByTestId(ids.picker)).toBeVisible();
  await expect(page.getByTestId(ids.pickerGroup)).toHaveText(['Recent', 'All your projects']);
  const keys = await page
    .getByTestId(ids.pickerOption)
    .evaluateAll((items) => items.map((item) => item.getAttribute('data-key')));
  expect(keys).toEqual(['SHIP', 'MOB', 'PAY']);

  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.picker)).toHaveCount(0);
});

test('Picker_WhenCannotCreate_ShouldHideNewProject', async ({ mount, page }) => {
  await stubShell(page, { me: meDto({ canCreateProjects: false }) });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await page.getByTestId(ids.projectButton).click();
  await expect(page.getByTestId(ids.pickerOption)).toHaveCount(3);
  await expect(page.getByTestId(ids.pickerNewProject)).toHaveCount(0);
  await expect(page.getByTestId(ids.pickerImport)).toHaveCount(0);
});

test('Picker_WhenCanCreate_ShouldOfferNewProjectAndImport', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await page.getByTestId(ids.projectButton).click();
  await page.getByTestId(ids.pickerNewProject).click();
  await expect(page.getByTestId(harness.location)).toHaveText('/projects/new');
  await page.getByTestId(ids.projectButton).click();
  await page.getByTestId(ids.pickerImport).click();
  await expect(page.getByTestId(harness.location)).toHaveText('/import');
});

test('Picker_WhenProjectPicked_ShouldOpenSameScreenThere', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/features?mode=tree' } });
  await page.getByTestId(ids.projectButton).click();
  await page.getByTestId(ids.pickerOption).filter({ hasText: 'Payments' }).click();
  await expect(page.getByTestId(harness.location)).toHaveText('/p/PAY/backlogs/features?mode=tree');
  await expect(page.getByTestId(ids.picker)).toHaveCount(0);
  await expect(page.getByTestId(ids.projectButton)).toContainText('Payments');
});

test('Picker_WhenPickedByKeyboardFromASprintPage_ShouldOpenAllSprintsThere', async ({
  mount,
  page,
}) => {
  await stubShell(page, { sprints: [sprint({ id: 's24' })] });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } });
  await expect(page.getByTestId(ids.projectButton)).toContainText('Shipping');
  await page.getByTestId(ids.projectButton).click();
  // Shipping (recent), then Mobile and Payments: down, down, up lands on the second.
  await expect(page.getByRole('option', { selected: true })).toHaveAttribute('data-key', 'SHIP');
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('option', { selected: true })).toHaveAttribute('data-key', 'MOB');
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('option', { selected: true })).toHaveAttribute('data-key', 'PAY');
  await page.keyboard.press('ArrowUp');
  await expect(page.getByRole('option', { selected: true })).toHaveAttribute('data-key', 'MOB');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId(harness.location)).toHaveText('/p/MOB/sprints');
});

test('UserMenu_WhenNotInstanceAdmin_ShouldHideInstanceSettings', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await page.getByTestId(ids.userButton).click();
  await expect(page.getByTestId(ids.userMenuName)).toHaveText('Priya Shah');
  await expect(page.getByTestId(ids.userMenuEmail)).toHaveText('priya@shiplean.local');
  await expect(page.getByTestId(ids.userMenuProfile)).toBeVisible();
  await expect(page.getByTestId(ids.userMenuInstance)).toHaveCount(0);
});

test('UserMenu_WhenInstanceAdmin_ShouldOfferInstanceSettingsWithAdminChip', async ({
  mount,
  page,
}) => {
  await stubShell(page, { me: meDto({ isInstanceAdmin: true }) });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await page.getByTestId(ids.userButton).click();
  await expect(page.getByTestId(ids.userMenuInstance)).toContainText('Admin');
  await page.getByTestId(ids.userMenuInstance).click();
  await expect(page.getByTestId(harness.location)).toHaveText('/instance/general');
  await expect(page.getByTestId(ids.userMenu)).toHaveCount(0);
});

test('UserMenu_WhenSignOutClicked_ShouldPostLogout', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  const logout = page.waitForRequest(
    (request) => request.url().endsWith('/api/auth/logout') && request.method() === 'POST',
  );
  await page.route('**/api/auth/logout', (route) => route.fulfill({ status: 204 }));
  await page.route('**/login?state=signed-out', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'text/html',
      body: `<p data-testid="${harness.signedOut}">signed out</p>`,
    }),
  );
  await page.getByTestId(ids.userButton).click();
  await page.getByTestId(ids.userMenuSignOut).click();
  await logout;
  await page.waitForURL('**/login?state=signed-out');
  await expect(page.getByTestId(harness.signedOut)).toHaveText('signed out');
});

test('Crumbs_WhenInstallLevel_ShouldStartAtShipLean', async ({ mount, page }) => {
  await stubShell(page, { recent: ['id-PAY'] });
  await mount(<ShellHarness />, { hooksConfig: { route: '/profile' } });
  await expect(page.getByTestId(ids.crumb)).toHaveText(['ShipLean', 'Your profile']);
  await expect(page.getByTestId(realtimeTestIds.liveDot)).toHaveCount(0);
  // The side menu keeps the project you came from.
  await expect(page.getByTestId(ids.projectButton)).toContainText('Payments');
  await expect(page.getByTestId(ids.navBacklogs)).toHaveAttribute(
    'href',
    '/p/PAY/backlogs/stories',
  );
});

test('Crumbs_WhenProjectScreen_ShouldStartAtTheProjectKey', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  await expect(page.getByTestId(ids.crumb)).toHaveText(['SHIP', 'Backlogs', 'Stories', 'Planning']);
  await expect(page.getByTestId(ids.navBacklogs)).toHaveAttribute('aria-current', 'page');
});

test('Crumbs_WhenUnknownAddress_ShouldShowJustTheRoot', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/nothing/here' } });
  await expect(page.getByTestId(ids.crumb)).toHaveText(['SHIP']);
});

test('SideMenu_WhenInSprints_ShouldListTheCurrentSprintAndAllSprints', async ({ mount, page }) => {
  await stubShell(page, {
    sprints: [
      sprint({ id: 's23', name: 'Sprint 23', status: 'past' }),
      sprint({ id: 's24', name: 'Sprint 24', status: 'current' }),
    ],
  });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } });
  await expect(page.getByTestId(ids.navCurrentSprint)).toContainText('Sprint 24');
  await expect(page.getByTestId(ids.navCurrentSprint)).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(ids.navAllSprints)).toHaveAttribute('href', '/p/SHIP/sprints');
  await expect(page.getByTestId(ids.navSprints)).toHaveAttribute('aria-current', 'page');
});

test('SideMenu_WhenNotInSprints_ShouldHideTheSprintItems', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/boards/stories' } });
  await expect(page.getByTestId(ids.navBoards)).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(ids.navAllSprints)).toHaveCount(0);
});

test('Shell_WhenProjectOpened_ShouldMoveItToTheFrontOfRecent', async ({ mount, page }) => {
  const saved: unknown[] = [];
  await stubShell(page, { recent: ['id-SHIP', 'id-MOB'], onRecentSaved: (ids) => saved.push(ids) });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/MOB/backlogs/stories' } });
  await expect.poll(() => saved).toEqual([['id-MOB', 'id-SHIP']]);
});

test('Shell_WhenTheProjectIsAlreadyFirstInRecent_ShouldNotSaveAgain', async ({ mount, page }) => {
  const saved: unknown[] = [];
  await stubShell(page, { recent: ['id-SHIP'], onRecentSaved: (ids) => saved.push(ids) });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId(ids.projectButton)).toContainText('Shipping');
  expect(saved).toEqual([]);
});

test('Shell_WhenPreferencesResolve_ShouldApplyThemeAndDensity', async ({ mount, page }) => {
  await stubShell(page, { me: meDto({ theme: 'dark', density: 'comfortable' }) });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect
    .poll(() => page.evaluate(() => document.documentElement.getAttribute('data-theme')))
    .toBe('dark');
  await expect
    .poll(() => page.evaluate(() => document.documentElement.getAttribute('data-density')))
    .toBe('comfortable');
});

test('Shell_WhenInstallLevelWithNoRecent_ShouldShowNoProjectSections', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/projects/new' } });
  await expect(page.getByTestId(ids.crumb)).toHaveText(['ShipLean', 'New project']);
  await expect(page.getByTestId(ids.navBacklogs)).toHaveCount(0);
  await expect(page.getByTestId(ids.projectButton)).toContainText('Choose a project');
});

test('Picker_WhenOnAnInstallLevelPage_ShouldOpenTheProjectTheMenuShows', async ({
  mount,
  page,
}) => {
  await stubShell(page, { recent: ['id-PAY'] });
  await mount(<ShellHarness />, { hooksConfig: { route: '/profile' } });
  await expect(page.getByTestId(ids.projectButton)).toContainText('Payments');
  await page.getByTestId(ids.projectButton).click();
  await page.getByTestId(ids.pickerOption).filter({ hasText: 'Payments' }).click();
  await expect(page.getByTestId(harness.location)).toHaveText('/p/PAY/backlogs/stories');
});

test('Picker_WhenTheRouteProjectIsPicked_ShouldStayWhereItIs', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/features?mode=tree' } });
  await page.getByTestId(ids.projectButton).click();
  await page.getByTestId(ids.pickerOption).filter({ hasText: 'Shipping' }).click();
  await expect(page.getByTestId(ids.picker)).toHaveCount(0);
  await expect(page.getByTestId(harness.location)).toHaveText(
    '/p/SHIP/backlogs/features?mode=tree',
  );
});

test('Shell_WhenProjectOpened_ShouldShowItFirstInThePickersRecent', async ({ mount, page }) => {
  let projectReads = 0;
  const saved: unknown[] = [];
  let release: () => void = () => undefined;
  const holdSaves = new Promise<void>((resolve) => {
    release = resolve;
  });
  await stubShell(page, {
    recent: ['id-SHIP', 'id-MOB'],
    holdSaves,
    onRecentSaved: (ids) => saved.push(ids),
    onProjectsRequested: () => (projectReads += 1),
  });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/MOB/backlogs/stories' } });
  // The picker is open before the save lands, so only the refreshes after the save can reorder it.
  await page.getByTestId(ids.projectButton).click();
  await expect(page.getByTestId(ids.pickerOption)).toHaveCount(3);
  // The first read, and the picker's own when it mounts.
  await expect.poll(() => projectReads).toBe(2);
  release();
  await expect.poll(() => saved).toEqual([['id-MOB', 'id-SHIP']]);
  await expect
    .poll(() =>
      page
        .getByTestId(ids.pickerOption)
        .evaluateAll((items) => items.map((item) => item.getAttribute('data-key'))),
    )
    .toEqual(['MOB', 'SHIP', 'PAY']);
  await expect.poll(() => projectReads).toBe(3);
});

test('Shell_WhenTwoProjectsAreOpenedQuickly_ShouldKeepBothInRecent', async ({ mount, page }) => {
  const saved: unknown[] = [];
  let release: () => void = () => undefined;
  const holdSaves = new Promise<void>((resolve) => {
    release = resolve;
  });
  await stubShell(page, {
    recent: ['id-SHIP'],
    onRecentSaved: (ids) => saved.push(ids),
    holdSaves,
  });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId(ids.projectButton)).toContainText('Shipping');
  await page.getByTestId(ids.projectButton).click();
  await page.getByTestId(ids.pickerOption).filter({ hasText: 'Mobile' }).click();
  await expect(page.getByTestId(harness.location)).toHaveText('/p/MOB/backlogs/stories');
  await page.getByTestId(ids.projectButton).click();
  await page.getByTestId(ids.pickerOption).filter({ hasText: 'Payments' }).click();
  await expect(page.getByTestId(harness.location)).toHaveText('/p/PAY/backlogs/stories');
  release();
  await expect
    .poll(() => saved)
    .toEqual([
      ['id-MOB', 'id-SHIP'],
      ['id-PAY', 'id-MOB', 'id-SHIP'],
    ]);
});

test('Shell_WhenOnAProjectRoute_ShouldResolveTheProjectForRealtime', async ({ mount, page }) => {
  const requested: string[] = [];
  await stubShell(page, { onByKeyRequested: (url) => requested.push(url) });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect.poll(() => requested.length).toBeGreaterThan(0);
});

test('Shell_WhenOnAnInstallLevelRoute_ShouldNotJoinAProjectHub', async ({ mount, page }) => {
  const requested: string[] = [];
  await stubShell(page, { recent: ['id-PAY'], onByKeyRequested: (url) => requested.push(url) });
  await mount(<ShellHarness />, { hooksConfig: { route: '/profile' } });
  await expect(page.getByTestId(ids.projectButton)).toContainText('Payments');
  await page.waitForLoadState('networkidle');
  expect(requested).toEqual([]);
});

test('Picker_WhenOpen_ShouldBeAComboboxOverTheListbox', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId(ids.projectButton)).toHaveAttribute('aria-haspopup', 'dialog');
  await page.getByTestId(ids.projectButton).click();
  const search = page.getByTestId(ids.pickerSearch);
  await expect(search).toHaveAttribute('role', 'combobox');
  const listId = await page.getByRole('listbox').getAttribute('id');
  await expect(search).toHaveAttribute('aria-controls', listId ?? 'missing');

  // The highlighted option is the one the combobox points at and the one marked selected.
  const first = page.getByTestId(ids.pickerOption).nth(0);
  await expect(search).toHaveAttribute(
    'aria-activedescendant',
    (await first.getAttribute('id')) ?? '',
  );
  await expect(first).toHaveAttribute('aria-selected', 'true');
  await page.keyboard.press('ArrowDown');
  const second = page.getByTestId(ids.pickerOption).nth(1);
  await expect(second).toHaveAttribute('aria-selected', 'true');
  await expect(first).toHaveAttribute('aria-selected', 'false');
  await expect(search).toHaveAttribute(
    'aria-activedescendant',
    (await second.getAttribute('id')) ?? '',
  );
});

test('Picker_WhenEscapePressedAwayFromTheSearch_ShouldClose', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await page.getByTestId(ids.projectButton).click();
  await page.getByTestId(ids.pickerNewProject).focus();
  await expect(page.getByTestId(ids.pickerNewProject)).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.picker)).toHaveCount(0);
});

test('Picker_WhenControlPPressed_ShouldNotOpen', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId(ids.projectButton)).toContainText('Shipping');
  // Fails on an implementation that keeps the picker's own Ctrl+P shortcut: it would
  // open the picker and move focus into its search, so the opener below is lost.
  await page.getByTestId(ids.navBoards).focus();
  await page.keyboard.press('Control+p');
  await expect(page.getByTestId(quickSearchTestIds.dialog)).toBeVisible();
  await expect(page.getByTestId(ids.picker)).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(quickSearchTestIds.dialog)).toHaveCount(0);
  await expect(page.getByTestId(ids.navBoards)).toBeFocused();
});

test('UserMenu_WhenSignOutFails_ShouldShowAToast', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await page.route('**/api/auth/logout', (route) =>
    route.fulfill({
      status: 400,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Nope.', code: 'validation' }),
    }),
  );
  await page.getByTestId(ids.userButton).click();
  await page.getByTestId(ids.userMenuSignOut).click();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Could not sign out');
});

test('UserMenu_WhenMeFails_ShouldOfferRetry', async ({ mount, page }) => {
  await stubShell(page, { meFails: true });
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId(ids.userRetry)).toBeVisible();
  await expect(page.getByTestId(ids.userButton)).toHaveCount(0);
  await page.unroute('**/api/me');
  await page.route('**/api/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(meDto()),
    }),
  );
  await page.getByTestId(ids.userRetry).click();
  await expect(page.getByTestId(ids.userButton)).toBeVisible();
});

test('UserMenu_WhenProxyModeWithSignOutUrl_ShouldPostLogoutThenGoToTheProxy', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  await stubProxyInstance(page, '/oauth2/sign_out');
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  const logout = page.waitForRequest(
    (request) => request.url().endsWith('/api/auth/logout') && request.method() === 'POST',
  );
  await page.route('**/api/auth/logout', (route) => route.fulfill({ status: 204 }));
  await page.route('**/oauth2/sign_out', (route) =>
    route.fulfill({ status: 200, contentType: 'text/html', body: '<html></html>' }),
  );
  await page.getByTestId(ids.userButton).click();
  await page.getByTestId(ids.userMenuSignOut).click();
  await logout;
  await page.waitForURL('**/oauth2/sign_out');
});

test('UserMenu_WhenProxyModeWithoutSignOutUrl_ShouldHideSignOut', async ({ mount, page }) => {
  await stubShell(page);
  await stubProxyInstance(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await page.getByTestId(ids.userButton).click();
  await expect(page.getByTestId(ids.userMenuProfile)).toBeVisible();
  await expect(page.getByTestId(ids.userMenuSignOut)).toHaveCount(0);
});

test('UserMenu_WhenTheInstanceReadFails_ShouldOfferSignOutAsInPasswordMode', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  const failed = page.waitForResponse('**/api/auth/instance');
  await page.route('**/api/auth/instance', (route) =>
    route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Nope.', code: 'internal' }),
    }),
  );
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await failed;
  await page.getByTestId(ids.userButton).click();
  await expect(page.getByTestId(ids.userMenuSignOut)).toBeVisible();
});
