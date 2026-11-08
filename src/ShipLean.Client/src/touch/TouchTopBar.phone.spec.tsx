import { expect, test, type Locator, type Page } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../AppRoutes';
import { realtimeTestIds } from '../features/realtime/testIds';
import { shellTestIds } from '../features/shell/testIds';
import { stubShell } from '../pages/screenshots/shellStub';
import { TopBarHarness } from './ct/TopBarHarness';
import { topBarHarnessIds as harness } from './ct/topBarHarnessIds';
import { touchTestIds as ids } from './testIds';

// The touch top bar and drawer (K4) on the phone project. The bar tests mount it with a page that fills its slots;
// the drawer tests mount the real shell. Baselines come from the CI image: scripts/update-page-shots.sh
// src/touch/TouchTopBar.phone.spec.tsx.

const ROUTE = '/p/SHIP/backlogs/stories';
const THEMES = ['light', 'dark'] as const;

async function lefts(page: Page, testIds: string[]): Promise<number[]> {
  const boxes = await Promise.all(testIds.map((id) => page.getByTestId(id).first().boundingBox()));
  return boxes.map((box) => box!.x);
}

function ascending(values: number[]): boolean {
  return values.every((value, index) => index === 0 || value > values[index - 1]!);
}

async function mountShell(mount: (c: React.JSX.Element, o: object) => Promise<Locator>, page: Page) {
  await stubShell(page);
  await mount(<AppRoutes />, { hooksConfig: { route: ROUTE } });
  await expect(page.getByTestId(ids.topBar)).toBeVisible();
}

test('TouchTopBar_WhenNoBack_ShouldShowMenuTitleRealtimeActionSearchInOrder', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<TopBarHarness variant="plain" />, { hooksConfig: { route: ROUTE } });
  await expect(page.getByTestId(ids.topBarTitle)).toContainText('Stories');
  await expect(page.getByTestId(realtimeTestIds.liveDot)).toBeVisible();

  const order = await lefts(page, [
    ids.topBarMenu,
    ids.topBarTitle,
    realtimeTestIds.liveDot,
    ids.topBarAction,
    ids.topBarSearch,
  ]);

  expect(ascending(order), `left edges ${order.join(', ')}`).toBe(true);
  await expect(page.getByTestId(ids.topBarBack)).toHaveCount(0);
});

test('TouchTopBar_WhenBackSet_ShouldPutBackLeftAndMenuRight', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<TopBarHarness variant="back" />, { hooksConfig: { route: ROUTE } });
  const back = page.getByTestId(ids.topBarBack);
  await expect(back).toHaveAttribute('aria-label', 'Back to Stories');
  await expect(back).toHaveAttribute('href', ROUTE);
  await expect(page.getByTestId(ids.topBarTitle)).toHaveText('SHIP-101');

  const order = await lefts(page, [
    ids.topBarBack,
    ids.topBarTitle,
    realtimeTestIds.liveDot,
    ids.topBarAction,
    ids.topBarSearch,
    ids.topBarMenu,
  ]);

  expect(ascending(order), `left edges ${order.join(', ')}`).toBe(true);
});

test('TouchTopBar_WhenPinnedSet_ShouldReplaceTheBar', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<TopBarHarness variant="pinned" />, { hooksConfig: { route: ROUTE } });

  const pinned = page.getByTestId(ids.topBarPinned);
  await expect(pinned.getByTestId(harness.ctTopBarPinnedDone)).toBeVisible();
  await expect(page.getByTestId(ids.topBar)).toHaveCount(0);
  await expect(page.getByTestId(ids.topBarMenu)).toHaveCount(0);
  expect((await pinned.boundingBox())!.y).toBe(0);
});

test('TouchTopBar_WhenThePageScrolls_ShouldStayAtTheTop', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<TopBarHarness variant="plain" />, { hooksConfig: { route: ROUTE } });
  const bar = page.getByTestId(ids.topBar);
  await expect(bar).toBeVisible();

  await page.evaluate(() => window.scrollTo(0, 600));
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(0);

  const box = (await bar.boundingBox())!;
  expect(box.y).toBe(0);
  expect(box.height).toBeGreaterThanOrEqual(48);
  for (const id of [ids.topBarMenu, ids.topBarSearch]) {
    const control = (await page.getByTestId(id).boundingBox())!;
    expect(Math.min(control.width, control.height)).toBeGreaterThanOrEqual(32);
  }
});

test('TouchDrawer_WhenMenuTapped_ShouldOpenOverTheScrim', async ({ mount, page }) => {
  await mountShell(mount, page);
  await expect(page.getByTestId(shellTestIds.sideMenu)).toHaveCount(0);

  await page.getByTestId(ids.topBarMenu).tap();

  const drawer = page.getByTestId(ids.drawer);
  await expect(drawer.getByTestId(shellTestIds.sideMenu)).toBeVisible();
  await expect(page.getByTestId(shellTestIds.sideMenu)).toHaveCount(1);
  await expect(page.getByTestId(ids.drawerScrim)).toBeVisible();
  const box = (await drawer.boundingBox())!;
  expect(box.x).toBe(0);
  expect(box.width).toBe(304);
  expect(box.height).toBe(844);
  const menu = (await drawer.getByTestId(shellTestIds.sideMenu).boundingBox())!;
  expect(menu.width).toBeGreaterThan(300);
  // What a tap would hit: the drawer over its own area, the scrim beside it.
  const hits = await page.evaluate(
    ({ drawer, scrim }) => [
      document.elementFromPoint(150, 400)?.closest(`[data-testid="${drawer}"]`) !== null,
      document.elementFromPoint(360, 400)?.getAttribute('data-testid') === scrim,
    ],
    { drawer: ids.drawer, scrim: ids.drawerScrim },
  );
  expect(hits).toEqual([true, true]);
});

test('TouchDrawer_WhenScrimEscOrCloseUsed_ShouldClose', async ({ mount, page }) => {
  await mountShell(mount, page);
  const drawer = page.getByTestId(ids.drawer);
  const menu = page.getByTestId(ids.topBarMenu);

  await menu.tap();
  await expect(drawer).toBeVisible();
  await page.getByTestId(ids.drawerScrim).tap({ position: { x: 360, y: 400 } });
  await expect(drawer).toHaveCount(0);

  await menu.tap();
  await expect(drawer).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(drawer).toHaveCount(0);

  await menu.tap();
  await expect(drawer).toBeVisible();
  await page.getByTestId(ids.drawerClose).tap();
  await expect(drawer).toHaveCount(0);
  await expect(page.getByTestId(ids.drawerScrim)).toHaveCount(0);
});

for (const theme of THEMES) {
  for (const variant of ['plain', 'back'] as const) {
    const name = variant === 'plain' ? 'touch-top-bar' : 'touch-top-bar-back';
    test(`TouchTopBar_WhenShown_ShouldMatchItsScreenshot_${name}_${theme}`, async ({ mount, page }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await stubShell(page);
      await mount(<TopBarHarness variant={variant} />, { hooksConfig: { route: ROUTE } });
      const bar = page.getByTestId(ids.topBar);
      await expect(page.getByTestId(realtimeTestIds.liveDot)).toBeVisible();

      await expect(bar).toHaveScreenshot(`${name}.${theme}.png`, { animations: 'disabled' });
    });
  }

  test(`TouchDrawer_WhenOpen_ShouldMatchItsScreenshot_touch-drawer_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mountShell(mount, page);
    await page.getByTestId(ids.topBarMenu).tap();
    const drawer = page.getByTestId(ids.drawer);
    await expect(drawer.getByTestId(shellTestIds.projectButton)).toContainText('Storefront');
    await expect(drawer.getByTestId(shellTestIds.userButton)).toContainText('Priya Shah');

    await expect(drawer).toHaveScreenshot(`touch-drawer.${theme}.png`, { animations: 'disabled' });
  });
}
