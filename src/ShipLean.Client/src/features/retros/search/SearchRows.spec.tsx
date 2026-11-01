import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubShell } from '../../../pages/screenshots/shellStub';
import { retrosTestIds as ids } from '../testIds';
import { retroSearch, stubRetros } from '../ct/retroFixtures';

const open = (route: string) => ({ hooksConfig: { route } });

test('SearchRows_WhenResultHasPoints_ShouldShowOneRowEachWithItsParts', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  const rows = page.getByTestId(ids.retrosRow);
  await expect(rows).toHaveCount(retroSearch.rows.length);
  const first = rows.first();
  const point = retroSearch.rows[0];
  await expect(first).toContainText('Went badly');
  await expect(first).toContainText(point.text);
  await expect(first).toContainText(`Root cause: ${point.rootCause}`);
  await expect(first).toContainText(point.theme!.name);
  await expect(first).toContainText('Recurring');
  await expect(first).toContainText(point.workItem!.key);
  await expect(first).toContainText(point.sprint!.name);
  await expect(first.locator('[title="Priya Shah · Claude Code · lane A"]')).toBeVisible();
});

test('SearchRows_WhenTextMatches_ShouldHighlightIt', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros?q=screenshot'));
  await expect(page.getByTestId(ids.retrosRows).locator('mark').first()).toHaveText(/screenshot/i);
});

test('SearchRows_WhenRowClicked_ShouldSetSelected', async ({ mount, page }) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  await page.getByTestId(ids.retrosRow).nth(1).click();
  await expect
    .poll(() => new URLSearchParams(stub.reads('/retros').at(-1)?.query).get('selected'))
    .toBe(retroSearch.rows[1].retroId);
});

test('SearchRows_WhenQueryMatchesOnlyTheLog_ShouldSayWhere', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros?q=masked%20PAN'));
  await expect(page.getByTestId(ids.retrosRows)).toContainText('Match in the raw failure log');
});

test('SearchRows_WhenResultIsEmpty_ShouldShowTheEmptyState', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await page.route(
    (url) => /^\/api\/projects\/[^/]+\/retros$/.test(url.pathname),
    (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ...retroSearch, rows: [], total: 0, selected: null }),
      }),
  );
  await mount(<AppRoutes />, open('/p/SHIP/retros?q=zzz'));
  await expect(page.getByTestId(ids.retrosEmpty)).toContainText('No point matches');
  await expect(page.getByTestId(ids.retrosRow)).toHaveCount(0);
});

for (const theme of ['light', 'dark'] as const) {
  test(`SearchRows_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubShell(page);
    await stubRetros(page);
    await mount(<AppRoutes />, open('/p/SHIP/retros?q=screenshot'));
    await expect(page.getByTestId(ids.retrosRows)).toHaveScreenshot(`search-rows.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
