import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { shellTestIds as ids } from '../../features/shell/testIds';
import { stubShell } from './shellStub';

// The side menu once Epics, Features and Stories live under Backlogs and Boards. Whole-page shots still cover the
// headers those tabs left; these crop the menu itself, including the narrow strip where the children hide. Baselines
// come from the CI image — regenerate with scripts/update-page-shots.sh sideMenu.

interface MenuShot {
  name: string;
  route: string;
  /** The level link that is current, so the shot waits for the submenu. */
  current: string;
  /** Under 900px the children hide and the menu becomes a strip. */
  narrow?: boolean;
}

const THEMES = ['light', 'dark'] as const;

const SHOTS: MenuShot[] = [
  { name: 'side-menu-backlogs', route: '/p/SHIP/backlogs/features', current: 'Features' },
  { name: 'side-menu-boards', route: '/p/SHIP/boards/stories', current: 'Stories' },
  { name: 'side-menu-narrow', route: '/p/SHIP/backlogs/stories', current: 'Stories', narrow: true },
];

for (const shot of SHOTS) {
  for (const theme of THEMES) {
    test(`SideMenu_WhenOpened_ShouldMatchItsScreenshot_${shot.name}_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.setViewportSize({ width: shot.narrow ? 800 : 1440, height: 900 });
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await stubShell(page);
      await mount(<AppRoutes />, { hooksConfig: { route: shot.route } });

      const menu = page.getByTestId(ids.sideMenu);
      await expect(menu.getByTestId(ids.projectButton)).toContainText('Storefront');
      await expect(menu.getByTestId(ids.userButton)).toContainText('Priya Shah');
      const current = menu.getByTestId(ids.navLevel).filter({ hasText: shot.current });
      if (shot.narrow) await expect(current).toBeHidden();
      else await expect(current).toHaveAttribute('aria-current', 'page');

      await expect(menu).toHaveScreenshot(`${shot.name}.${theme}.png`, { animations: 'disabled' });
    });
  }
}
