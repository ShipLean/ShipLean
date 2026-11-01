import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubShell } from '../../../pages/screenshots/shellStub';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { retrosTestIds as ids } from '../testIds';
import { retroSearch, stubRetros } from '../ct/retroFixtures';

const open = (route: string) => ({ hooksConfig: { route } });

test('SelectedRetro_WhenARetroIsSelected_ShouldShowItsCardWithTheOpenedPointHighlighted', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  const selected = page.getByTestId(ids.retrosSelected);
  await expect(selected.getByTestId(shared.retroCard)).toContainText(
    retroSearch.selected!.workItem!.title,
  );
  await expect(selected.locator('[data-highlighted="true"]')).toHaveCount(1);
  await expect(selected.getByTestId(shared.retroCardLog)).toHaveCount(0);
});

test('SelectedRetro_WhenTheMatchIsInTheLog_ShouldOpenTheFailureLog', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros?q=masked%20PAN'));
  await expect(page.getByTestId(ids.retrosSelected).getByTestId(shared.retroCardLog)).toBeVisible();
});

test('SelectedRetro_WhenTheMatchIsInAPoint_ShouldLeaveTheLogCollapsed', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros?q=baseline'));
  await expect(page.getByTestId(ids.retrosSelected).getByTestId(shared.retroCard)).toBeVisible();
  await expect(page.getByTestId(shared.retroCardLog)).toHaveCount(0);
});

test('SelectedRetro_WhenNothingIsSelected_ShouldInviteASelection', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await page.route(
    (url) => /^\/api\/projects\/[^/]+\/retros$/.test(url.pathname),
    (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ...retroSearch, selected: null }),
      }),
  );
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  await expect(page.getByTestId(ids.retrosSelected)).toContainText('Select a point');
});

for (const theme of ['light', 'dark'] as const) {
  test(`SelectedRetro_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubShell(page);
    await stubRetros(page);
    await mount(<AppRoutes />, open('/p/SHIP/retros?q=screenshot'));
    await expect(page.getByTestId(ids.retrosSelected)).toHaveScreenshot(
      `selected-retro.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
