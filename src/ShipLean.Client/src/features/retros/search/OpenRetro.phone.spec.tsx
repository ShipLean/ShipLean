import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubShell } from '../../../pages/screenshots/shellStub';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { componentTestIds } from '../../../components/testIds';
import { touchTestIds } from '../../../touch/testIds';
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
    await expect(page.getByTestId(componentTestIds.dialogSubtitle)).toContainText('Result 1 of');
    await expect(page.locator('[data-highlighted="true"]')).toHaveCount(1);
    await expect(page).toHaveScreenshot(`retro-search-retro-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`OpenRetroPhone_WhenThePointsThemeIsTapped_ShouldMatchItsScreenshot_retro-search-theme-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubShell(page);
    await stubRetros(page);
    await mount(<AppRoutes />, { hooksConfig: { route } });
    await expect(page.getByTestId(ids.retrosOpenRetro)).toBeVisible();
    await page.locator('[data-highlighted="true"]').getByTestId(shared.retroNoteTheme).tap();
    await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Move to theme');
    await expect(page.getByTestId(shared.retroNotePreview)).toBeVisible();
    await expect(page).toHaveScreenshot(`retro-search-theme-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`OpenRetroPhone_WhenTheNoteMoreIsTapped_ShouldMatchItsScreenshot_retro-search-note-phone_${theme}`, async ({
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
        body: JSON.stringify({
          ...retroSearch,
          selected: {
            ...retroSearch.selected,
            points: retroSearch.selected!.points.map((p) =>
              p.id === point.id ? { ...p, canArchive: true } : p,
            ),
          },
        }),
      }),
    );
    await mount(<AppRoutes />, { hooksConfig: { route } });
    await expect(page.getByTestId(ids.retrosOpenRetro)).toBeVisible();
    await page.locator('[data-highlighted="true"]').getByTestId(touchTestIds.moreButton).tap();
    await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    await expect(page).toHaveScreenshot(`retro-search-note-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`OpenRetroPhone_WhenAddActionItemIsTapped_ShouldMatchItsScreenshot_retro-search-action-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubShell(page);
    await stubRetros(page);
    await mount(<AppRoutes />, { hooksConfig: { route } });
    await expect(page.getByTestId(ids.retrosOpenRetro)).toBeVisible();
    await page.locator('[data-highlighted="true"]').getByTestId(shared.retroNoteAddAction).tap();
    await expect(page.getByTestId(shared.retroActionCreate)).toBeVisible();
    await expect(page).toHaveScreenshot(`retro-search-action-phone.${theme}.png`, {
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
    const log = page.getByTestId(shared.retroCardLog);
    await expect(log).toBeVisible();
    // The raw log sits below the fold; bring it up so the shot shows it open.
    await log.scrollIntoViewIfNeeded();
    await expect(log).toBeInViewport();
    await expect(page).toHaveScreenshot(`retro-search-retro-log-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

test('OpenRetroHeader_ShouldShowTheResultPosition', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, { hooksConfig: { route } });
  const dialog = page.getByTestId(ids.retrosOpenRetro);
  await expect(dialog).toBeVisible();
  const item = retroSearch.selected!.workItem!;
  const typeKey = dialog.getByTestId(ids.retrosOpenRetroTypeKey);
  const key = dialog.getByTestId(ids.retrosOpenRetroKey);
  const title = dialog.getByTestId(componentTestIds.dialogTitle);
  const subtitle = dialog.getByTestId(componentTestIds.dialogSubtitle);
  await expect(typeKey.getByTestId(componentTestIds.typeIcon)).toHaveAttribute(
    'data-type',
    item.type.id,
  );
  await expect(key).toHaveText(item.key);
  await expect(title).toHaveText(item.title);
  await expect(title).not.toContainText(item.type.name);
  // The position sits in the header, under the icon, and the whole of line 1 is the dialog's name.
  await expect(subtitle).toContainText(/^Result 1 of \d+/);
  await expect(subtitle).toContainText(retroSearch.selected!.sprint!.name);
  await expect(dialog).toHaveAccessibleName(new RegExp(`${item.key}.*${item.title}`));
  await expect(dialog).toHaveAccessibleDescription(/^Result 1 of/);
  const iconBox = (await typeKey.boundingBox())!;
  const titleBox = (await title.boundingBox())!;
  const subtitleBox = (await subtitle.boundingBox())!;
  expect(subtitleBox.y).toBeGreaterThanOrEqual(titleBox.y + titleBox.height - 1);
  expect(Math.abs(subtitleBox.x - iconBox.x)).toBeLessThan(1);
  // A long title truncates instead of pushing the step buttons off the screen.
  const steps = dialog.getByTestId(ids.retrosOpenRetroSteps);
  await expect(steps).toBeInViewport({ ratio: 1 });
  await expect(title).toHaveCSS('text-overflow', 'ellipsis');
  // The ‹ › pair is bordered.
  await expect(steps).toHaveCSS('border-top-width', '1px');
  await expect(steps.getByTestId(ids.retrosOpenRetroPrevious)).toBeVisible();
  await expect(steps.getByTestId(ids.retrosOpenRetroNext)).toBeVisible();
});
