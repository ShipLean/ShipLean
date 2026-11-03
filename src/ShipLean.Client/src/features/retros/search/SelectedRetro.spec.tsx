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

test('SelectedRetro_WhenARetroIsSelected_ShouldGiveItsNotesTheirButtonsAndAnAddButton', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  const selected = page.getByTestId(ids.retrosSelected);
  await expect(selected.getByTestId(shared.retroBoard)).toBeVisible();
  await expect(selected.getByTestId(shared.retroNote).first()).toBeVisible();
  await expect(selected.getByTestId(shared.retroBoardAdd).first()).toBeVisible();
  // A went-badly or improve note that nobody has decided on offers both decisions.
  await expect(selected.getByTestId(shared.retroNoteAddAction).first()).toBeVisible();
  await expect(selected.getByTestId(shared.retroNoteNotNeeded).first()).toBeVisible();
});

const searchRoute = (url: URL) => /^\/api\/projects\/[^/]+\/retros$/.test(url.pathname);

test('SelectedRetro_WhenTheHighlightedPointMatchedOnlyInTheLog_ShouldOpenTheFailureLog', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  await stubRetros(page);
  await page.route(searchRoute, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ ...retroSearch, logMatchPointIds: [retroSearch.rows[0].id] }),
    }),
  );
  await mount(<AppRoutes />, open('/p/SHIP/retros?q=masked%20PAN'));
  await expect(page.getByTestId(ids.retrosSelected).getByTestId(shared.retroCardLog)).toBeVisible();
});

test('SelectedRetro_WhenTwoRowsShareARetro_ShouldHighlightTheClickedPoint', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  await stubRetros(page);
  const retro = retroSearch.selected!;
  const [first, second] = retro.points.filter((point) => point.id !== retroSearch.rows[0].id);
  await page.route(searchRoute, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ...retroSearch,
        rows: [first, second],
        total: 2,
        logMatchPointIds: [],
      }),
    }),
  );
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  const rows = page.getByTestId(ids.retrosRow);
  await expect(rows.first()).toHaveAttribute('aria-selected', 'true');
  await rows.nth(1).click();
  await expect(rows.nth(1)).toHaveAttribute('aria-selected', 'true');
  await expect(rows.first()).toHaveAttribute('aria-selected', 'false');
  await expect(
    page.getByTestId(ids.retrosSelected).locator('[data-highlighted="true"]'),
  ).toContainText(second.text);
});

test('SelectedRetro_WhenTheCardHasAnItemAndASprint_ShouldLinkToThem', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  const selected = page.getByTestId(ids.retrosSelected);
  const card = retroSearch.selected!;
  await expect(selected.getByRole('link', { name: `Open ${card.workItem!.key}` })).toBeVisible();
  await expect(selected.getByRole('link', { name: 'Sprint retro' })).toHaveAttribute(
    'href',
    `/p/SHIP/sprints/${card.sprint!.id}/retro`,
  );
});

test('SelectedRetro_WhenTheCardHasNoItemOrSprint_ShouldHideTheButtons', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await page.route(searchRoute, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ...retroSearch,
        selected: { ...retroSearch.selected, workItem: null, sprint: null },
      }),
    }),
  );
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  const selected = page.getByTestId(ids.retrosSelected);
  await expect(selected.getByTestId(shared.retroCard)).toBeVisible();
  await expect(selected.getByRole('link', { name: 'Sprint retro' })).toHaveCount(0);
  await expect(selected.getByRole('link', { name: /^Open / })).toHaveCount(0);
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
