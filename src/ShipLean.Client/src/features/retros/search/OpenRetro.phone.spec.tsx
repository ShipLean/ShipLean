import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubShell } from '../../../pages/screenshots/shellStub';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { retroSearch, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';

// The open retro on a phone (SHIP-730): the whole retro full screen at the clicked point, ‹ › at the top right.
// Regenerate the baselines with scripts/update-page-shots.sh src/features/retros/search/OpenRetro.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const point = retroSearch.rows[0];
const route = `/p/SHIP/retros?q=masked&selected=${retroSearch.selected!.id}&point=${point.id}`;
const searchRoute = (url: URL) => /^\/api\/projects\/[^/]+\/retros$/.test(url.pathname);

for (const theme of THEMES) {
  test(`OpenRetroPhone_WhenOpenedAtAPoint_ShouldMatchItsScreenshot_retro-search-retro-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubShell(page);
    await stubRetros(page);
    await mount(<AppRoutes />, { hooksConfig: { route } });
    await expect(page.getByTestId(ids.retrosOpenRetro)).toBeVisible();
    await expect(page.getByTestId(ids.retrosOpenRetroPosition)).toContainText('Result 1 of');
    await expect(page.locator('[data-highlighted="true"]')).toHaveCount(1);
    await expect(page).toHaveScreenshot(`retro-search-retro-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`OpenRetroPhone_WhenMatchedOnlyInTheLog_ShouldMatchItsScreenshot_retro-search-retro-log-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubShell(page);
    await stubRetros(page);
    await page.route(searchRoute, (r) =>
      r.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ...retroSearch, logMatchPointIds: [point.id] }),
      }),
    );
    await mount(<AppRoutes />, { hooksConfig: { route } });
    await expect(page.getByTestId(ids.retrosOpenRetro)).toBeVisible();
    await expect(page.getByTestId(shared.retroCardLog)).toBeVisible();
    await expect(page).toHaveScreenshot(`retro-search-retro-log-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
