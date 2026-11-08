import { expect, test } from '@playwright/experimental-ct-react';
import { stubShell } from '../../../pages/screenshots/shellStub';
import { touchTestIds as touch } from '../../../touch/testIds';
import { ShellHarness } from '../ct/ShellHarness';
import { shellTestIds as ids } from '../testIds';

// The drawer's body on the phone project (SHIP-713): the side menu with every section's sub-pages, the user menu opening
// upwards from the drawer's bottom, and the project picker as a bottom sheet whose field waits for a tap.

type Fixtures = Parameters<Parameters<typeof test>[2]>[0];

async function openDrawer({ mount, page }: Pick<Fixtures, 'mount' | 'page'>, route: string) {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route } });
  await page.getByTestId(touch.topBarMenu).tap();
  const drawer = page.getByTestId(touch.drawer);
  await expect(drawer.getByTestId(ids.userButton)).toBeVisible();
  return drawer;
}

test('TouchDrawer_WhenOnSprints_ShouldShowCurrentSprintAndAllSprints', async ({ mount, page }) => {
  const drawer = await openDrawer({ mount, page }, '/p/SHIP/sprints');

  await expect(drawer.getByTestId(ids.navCurrentSprint)).toContainText('Sprint 24');
  await expect(drawer.getByTestId(ids.navAllSprints)).toHaveAttribute('aria-current', 'page');
  // The other sections' sub-pages show too, though you are not in them: Epics · Features · Stories for Backlogs and
  // for Boards, Search · Features · Insights for Retrospectives.
  await expect(drawer.getByTestId(ids.navLevel)).toHaveText([
    'Epics',
    'Features',
    'Stories',
    'Epics',
    'Features',
    'Stories',
  ]);
  await expect(drawer.getByTestId(ids.navRetrosSearch)).toBeVisible();
  await expect(drawer.getByTestId(ids.navRetrosFeatures)).toBeVisible();
  await expect(drawer.getByTestId(ids.navRetrosInsights)).toBeVisible();
  const rows = await drawer.locator('a').all();
  for (const row of rows) {
    expect((await row.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }
});

test('TouchDrawer_WhenUserButtonTapped_ShouldOpenMenuUpwards', async ({ mount, page }) => {
  const drawer = await openDrawer({ mount, page }, '/p/SHIP/backlogs/stories');
  const user = drawer.getByTestId(ids.userButton);
  const drawerBox = (await drawer.boundingBox())!;
  const userBox = (await user.boundingBox())!;
  // The user sits at the drawer's bottom (inside the side menu's 8px padding).
  expect(userBox.y + userBox.height).toBeGreaterThanOrEqual(drawerBox.y + drawerBox.height - 8);

  await user.tap();

  const menu = drawer.getByTestId(ids.userMenu);
  await expect(menu.getByTestId(ids.userMenuName)).toHaveText('Priya Shah');
  const menuBox = (await menu.boundingBox())!;
  expect(menuBox.y + menuBox.height).toBeLessThanOrEqual(userBox.y);
  expect(menuBox.x).toBeGreaterThanOrEqual(drawerBox.x);
  expect(menuBox.x + menuBox.width).toBeLessThanOrEqual(drawerBox.x + drawerBox.width);
  for (const id of [ids.userMenuProfile, ids.userMenuSignOut]) {
    expect((await menu.getByTestId(id).boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }

  // Esc puts the menu away and leaves the drawer open.
  await page.keyboard.press('Escape');
  await expect(menu).toHaveCount(0);
  await expect(drawer).toBeVisible();
});

test('ProjectPickerSheet_WhenOpened_ShouldNotFocusSearch', async ({ mount, page }) => {
  const drawer = await openDrawer({ mount, page }, '/p/SHIP/backlogs/stories');

  await drawer.getByTestId(ids.projectButton).tap();

  const sheet = page.getByTestId(touch.sheet);
  await expect(sheet.getByTestId(touch.sheetTitle)).toHaveText('Switch project');
  const search = sheet.getByTestId(ids.pickerSearch);
  await expect(search).toBeVisible();
  await expect(search).not.toBeFocused();
  await expect(sheet.getByTestId(ids.pickerOption)).toHaveCount(1);
  const footer = sheet.getByTestId(touch.sheetFooter);
  await expect(footer.getByTestId(ids.pickerNewProject)).toBeVisible();
  await expect(footer.getByTestId(ids.pickerImport)).toBeVisible();
  // The desktop popover is not there as well.
  await expect(drawer.getByTestId(ids.picker)).toHaveCount(0);

  // A tap inside the sheet keeps it open and focuses the field.
  await search.tap();
  await expect(search).toBeFocused();
  await expect(sheet).toBeVisible();

  await sheet.getByTestId(touch.sheetClose).tap();
  await expect(sheet).toHaveCount(0);
  await expect(drawer).toBeVisible();
});
