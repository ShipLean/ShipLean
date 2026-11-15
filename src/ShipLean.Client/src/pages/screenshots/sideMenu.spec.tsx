import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { shellTestIds as ids } from '../../features/shell/testIds';
import { stubShell } from './shellStub';

// The side menu once Epics, Features and Stories live under Backlogs and Boards. Whole-page shots still cover the
// headers those tabs left; these crop the menu itself. At 900px and below the menu sits in the touch drawer, whose shot
// is touch-drawer in src/touch/TouchTopBar.phone.spec.tsx. Baselines come from the CI image — regenerate with
// scripts/update-page-shots.sh sideMenu.

interface MenuShot {
  name: string;
  route: string;
  /** The level link that is current, so the shot waits for the submenu. */
  current: string;
}

const THEMES = ['light', 'dark'] as const;

const SHOTS: MenuShot[] = [
  { name: 'side-menu-backlogs', route: '/p/SHIP/backlogs/features', current: 'Features' },
  { name: 'side-menu-boards', route: '/p/SHIP/boards/stories', current: 'Stories' },
];

for (const shot of SHOTS) {
  for (const theme of THEMES) {
    test(`SideMenu_WhenOpened_ShouldMatchItsScreenshot_${shot.name}_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.setViewportSize({ width: 1440, height: 900 });
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await stubShell(page);
      await mount(<AppRoutes />, { hooksConfig: { route: shot.route } });

      const menu = page.getByTestId(ids.sideMenu);
      await expect(menu.getByTestId(ids.projectButton)).toContainText('Storefront');
      await expect(menu.getByTestId(ids.userButton)).toContainText('Priya Shah');
      const current = menu.getByTestId(ids.navLevel).filter({ hasText: shot.current });
      await expect(current).toHaveAttribute('aria-current', 'page');

      await expect(menu).toHaveScreenshot(`${shot.name}.${theme}.png`, { animations: 'disabled' });
    });
  }
}
