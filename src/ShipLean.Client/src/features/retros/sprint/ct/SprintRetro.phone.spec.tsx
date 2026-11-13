import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { AppRoutes } from '../../../../AppRoutes';
import { stubShell } from '../../../../pages/screenshots/shellStub';
import { retroSharedTestIds as shared } from '../../../retroShared/testIds';
import { stubRetros } from '../../ct/retroFixtures';
import { retrosTestIds as ids } from '../../testIds';

async function open(
  page: Page,
  mount: (c: React.JSX.Element, o: object) => Promise<unknown>,
  query = '',
) {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, {
    hooksConfig: { route: `/p/SHIP/sprints/sprint-24/retro${query}` },
  });
  await expect(page.getByTestId(shared.retroNote).first()).toBeVisible();
}

test('SprintRetro_WhenOnPhone_ShouldHideRailAndNotStickToolbar', async ({ mount, page }) => {
  await open(page, mount);
  await expect(page.getByTestId(ids.retroRail)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroHeader)).toHaveCount(0);
  const toolbar = page.getByTestId(ids.retroToolbar);
  expect(await toolbar.evaluate((el) => getComputedStyle(el).position)).toBe('static');
  // Written by and To decide move into the Filter sheet.
  await expect(toolbar.getByTestId(ids.retroWrittenByFilter)).toHaveCount(0);
  await expect(toolbar.getByTestId(ids.retroToDecideFilter)).toHaveCount(0);

  // The four numbers: one row of 52px buttons.
  const numbers = page.getByTestId(ids.retroNumbers).getByRole('button');
  await expect(numbers).toHaveCount(4);
  const boxes = await Promise.all((await numbers.all()).map((n) => n.boundingBox()));
  for (const box of boxes) {
    expect(box!.height).toBeGreaterThanOrEqual(52);
    expect(Math.abs(box!.y - boxes[0]!.y)).toBeLessThanOrEqual(1);
  }

  // The page opens on Went well, the only column on screen, with the tiles 2 × 2.
  const columns = page.getByTestId(shared.retroBoardColumn);
  await expect(columns).toHaveCount(1);
  await expect(columns).toHaveAttribute('data-category', 'wentWell');
  const tiles = page.getByTestId(ids.retroCategoryTile);
  await expect(tiles).toHaveCount(4);
  await expect(tiles.nth(0)).toHaveAttribute('aria-pressed', 'true');
  const tileBoxes = await Promise.all((await tiles.all()).map((t) => t.boundingBox()));
  expect(Math.abs(tileBoxes[0]!.y - tileBoxes[1]!.y)).toBeLessThanOrEqual(1);
  expect(tileBoxes[2]!.y).toBeGreaterThan(tileBoxes[0]!.y + tileBoxes[0]!.height - 1);
  const sideways = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(sideways).toBeLessThanOrEqual(0);
});

test('SprintRetro_WhenOnTablet_ShouldShowTilesInOneRowAndNotesInTwoColumns', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 768, height: 1024 });
  await open(page, mount, '?cat=wentBadly');
  await expect(page.getByTestId(ids.retroRail)).toHaveCount(0);
  const tiles = page.getByTestId(ids.retroCategoryTile);
  const tileBoxes = await Promise.all((await tiles.all()).map((t) => t.boundingBox()));
  expect(tileBoxes).toHaveLength(4);
  for (const box of tileBoxes) expect(Math.abs(box!.y - tileBoxes[0]!.y)).toBeLessThanOrEqual(1);

  const column = page.getByTestId(shared.retroBoardColumn);
  await expect(column).toHaveCount(1);
  await expect(column).toHaveAttribute('data-category', 'wentBadly');
  // Screenshot baselines need CI holds two Went badly notes: side by side on a tablet.
  const notes = column.getByTestId(shared.retroNote);
  const [first, second] = await Promise.all([
    notes.nth(0).boundingBox(),
    notes.nth(1).boundingBox(),
  ]);
  expect(Math.abs(first!.y - second!.y)).toBeLessThanOrEqual(1);
  expect(second!.x).toBeGreaterThan(first!.x + first!.width - 1);
});

for (const theme of ['light', 'dark'] as const) {
  test(`SprintRetro_WhenOnPhone_ShouldMatchTilesScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(page, mount);
    await expect(page.getByTestId(ids.retroCategoryTiles)).toHaveScreenshot(
      `sprint-retro-tiles-phone.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
