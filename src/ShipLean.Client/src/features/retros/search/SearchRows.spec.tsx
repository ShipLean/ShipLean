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
  await expect(first).not.toContainText('Root cause');
  await expect(first).toContainText(point.theme!.name);
  await expect(first.locator('[title="Recurring"]')).toHaveText('↻');
  await expect(first).toContainText(point.workItem!.key);
  await expect(first).toContainText(point.sprint!.name);
  await expect(first.locator('[title="Priya Shah · Claude Code · lane A"]')).toBeVisible();
});

test('SearchRows_WhenTheSearchMatchedTheRootCause_ShouldShowItOnlyThen', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  await stubRetros(page);
  const point = retroSearch.rows[0];
  const word = point.rootCause!.split(' ')[3];
  await mount(<AppRoutes />, open(`/p/SHIP/retros?q=${word}`));
  const rows = page.getByTestId(ids.retrosRow);
  await expect(rows.first()).toContainText(`Root cause: ${point.rootCause}`);
  // Another row's root cause does not share the word, so it stays out.
  await expect(rows.nth(1)).not.toContainText('Root cause');
});

test('SearchRows_WhenAnotherFieldMatchedAndOnlyOneWordIsInTheRootCause_ShouldKeepTheRootCauseOut', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  await stubRetros(page);
  const point = retroSearch.rows[0];
  // "brief" is in the root cause, "Pay" only in the item title: the search matched the title.
  const q = encodeURIComponent(
    `${point.rootCause!.split(' ')[1]} ${point.workItem!.title.split(' ')[0]}`,
  );
  await mount(<AppRoutes />, open(`/p/SHIP/retros?q=${q}`));
  const rows = page.getByTestId(ids.retrosRow);
  await expect(rows).toHaveCount(retroSearch.rows.length);
  await expect(rows.first()).not.toContainText('Root cause');
  // The hover title leaves it out as well.
  await expect(rows.first().locator('td').nth(1)).not.toHaveAttribute('title', /Root cause/);
});

test('SearchRows_WhenTextIsLong_ShouldStayOneLineAt1280', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  const rows = page.getByTestId(ids.retrosRow);
  await expect(rows).toHaveCount(retroSearch.rows.length);
  // One line is the height of the avatar and padding; two lines of text would add a line more to some rows.
  const heights = await Promise.all(
    (await rows.all()).map(async (row) => (await row.boundingBox())!.height),
  );
  expect(Math.max(...heights)).toBeLessThan(40);
  expect(Math.max(...heights) - Math.min(...heights)).toBeLessThan(1);
  // The theme name is cut with an ellipsis too, so it is on hover.
  const themed = retroSearch.rows[0].theme!;
  await expect(rows.first().locator('td').nth(2)).toHaveAttribute('title', themed.name);
  // The text is cut with an ellipsis, so the point's whole text is on hover.
  const cell = rows.first().locator('td').nth(1);
  await expect(cell).toHaveAttribute('title', new RegExp(retroSearch.rows[0].text.slice(0, 20)));
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
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

test('SearchRows_WhenARowIsInLogMatchPointIds_ShouldSayWhere', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros?q=masked%20PAN'));
  const rows = page.getByTestId(ids.retrosRow);
  await expect(rows.nth(1)).toContainText('Match in the raw failure log');
  await expect(rows.first()).not.toContainText('Match in the raw failure log');
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

// Owner P1: these screenshot baselines are stale (rows, filters and the selected retro changed in Q1); regenerate them in the CI Docker image.
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
