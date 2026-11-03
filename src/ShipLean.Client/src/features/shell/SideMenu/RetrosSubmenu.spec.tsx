import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubShell } from '../../../pages/screenshots/shellStub';
import { stubRetros } from '../../retros/ct/retroFixtures';
import { shellTestIds as shell } from '../testIds';

test('RetrosSubmenu_WhenOpened_ShouldOrderSearchFeaturesInsights', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/retros' } });

  const items = page
    .getByTestId(shell.sideMenu)
    .locator(
      [shell.navRetrosSearch, shell.navRetrosFeatures, shell.navRetrosInsights]
        .map((id) => `[data-testid="${id}"]`)
        .join(','),
    );
  await expect(items).toHaveCount(3);
  const order = await items.evaluateAll((els) => els.map((el) => el.getAttribute('data-testid')));
  expect(order).toEqual([shell.navRetrosSearch, shell.navRetrosFeatures, shell.navRetrosInsights]);
});

test('RetrosSubmenu_WhenOnAnItemScreen_ShouldLightFeatures', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/retros/items/SHIP-141' } });

  await expect(page.getByTestId(shell.navRetrosFeatures)).toHaveAttribute('aria-current', 'page');
});
