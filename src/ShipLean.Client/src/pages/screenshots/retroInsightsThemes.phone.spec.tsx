import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { retroInsights, stubRetros } from '../../features/retros/ct/retroFixtures';
import { retrosTestIds as ids } from '../../features/retros/testIds';
import { touchTestIds } from '../../touch/testIds';
import { stubShell } from './shellStub';

// Recurring themes on a phone inside the app (SHIP-731 D1, D4), as element shots of this task's regions only (one card,
// the theme sheet), so the toolbar, the callouts and the Unthemed card, which other tasks build on the same page, don't
// move them. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/retroInsightsThemes.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const helped = retroInsights.themes.find((row) => row.lastVerdict === 'helped')!;

for (const theme of THEMES) {
  test(`PagePhone_WhenOpened_ShouldMatchItsScreenshot_retro-insights-themes-card_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubShell(page);
    await stubRetros(page);
    await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/retros/insights' } });
    const card = page
      .getByTestId(ids.retroThemeCard)
      .and(page.locator(`[data-theme-id="${helped.theme.id}"]`));
    await expect(card).toBeVisible();
    await expect(card).toHaveScreenshot(`retro-insights-themes-card.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`PagePhone_WhenASheetIsOpen_ShouldMatchItsScreenshot_retro-insights-themes-sheet_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubShell(page);
    await stubRetros(page);
    await mount(<AppRoutes />, {
      hooksConfig: { route: `/p/SHIP/retros/insights?sheet=theme:${helped.theme.id}` },
    });
    await expect(page.getByTestId(ids.retroThemeSheetMention)).toHaveCount(3);
    await expect(page.getByTestId(ids.retroThemeSheetAddAction)).toContainText('open point');
    await expect(page.getByTestId(touchTestIds.sheet)).toHaveScreenshot(
      `retro-insights-themes-sheet.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
