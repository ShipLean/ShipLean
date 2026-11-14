import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { touchTestIds } from '../../../touch/testIds';
import { retroItem, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { RetroItemPanel } from './RetroItemPanel';

// One item's retros on a phone (SHIP-729): the header numbers as buttons, one toolbar row, the board stacked, the rail as
// sheets and a Feature's stories as row links. Regenerate the baselines with
// scripts/update-page-shots.sh src/features/retros/item/RetroItem.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const route = (query = '') => ({ hooksConfig: { route: `/p/SHIP/retros/items/SHIP-101${query}` } });

async function serve(page: Parameters<typeof stubRetros>[0]) {
  await stubRetros(page);
  await page.route(/\/api\/projects\/[^/]+\/retros\/items\/[^/]+$/, (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(retroItem) }),
  );
}

for (const theme of THEMES) {
  test(`RetroItemPhone_WhenOpened_ShouldMatchItsScreenshot_retro-item-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await serve(page);
    const screen = await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
    await expect(page.getByTestId(ids.retroItemKpiToDecide)).toBeVisible();
    await expect(screen).toHaveScreenshot(`retro-item-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`RetroItemPhone_WhenAFeature_ShouldMatchItsScreenshot_retro-item-phone-feature_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await serve(page);
    await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
    const rows = page.getByTestId(ids.retroItemChildRows);
    await expect(rows).toBeVisible();
    await expect(rows).toHaveScreenshot(`retro-item-phone-feature.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`RetroItemPhone_WhenActionItemsOpened_ShouldMatchItsScreenshot_retro-item-phone-sheet_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await serve(page);
    await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
    await page.getByTestId(ids.retroItemKpiActions).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet).toBeVisible();
    await expect(sheet).toHaveScreenshot(`retro-item-phone-sheet.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

test('RetroItemPhone_WhenOpened_ShouldStackTheCategoriesAndLeaveTheRailToSheets', async ({
  mount,
  page,
}) => {
  await serve(page);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  await expect(page.getByTestId(ids.retroItemRail)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroItemFilter)).toHaveCount(0);
  const columns = await page
    .getByTestId(shared.retroBoardColumn)
    .evaluateAll((els) => els.map((el) => Math.round(el.getBoundingClientRect().x)));
  expect(new Set(columns).size).toBe(1);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    ),
  ).toBe(true);
  for (const id of [ids.retroItemKpiToDecide, ids.retroItemKpiActions, ids.retroItemKpiDecided]) {
    expect((await page.getByTestId(id).boundingBox())!.height).toBeGreaterThanOrEqual(52);
  }
});

test('RetroItemPhone_WhenToDecideTapped_ShouldToggleTheFilterAndPress', async ({ mount, page }) => {
  await serve(page);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  const kpi = page.getByTestId(ids.retroItemKpiToDecide);
  await expect(kpi).toHaveAttribute('aria-pressed', 'false');
  await kpi.tap();
  await expect(kpi).toHaveAttribute('aria-pressed', 'true');
  await kpi.tap();
  await expect(kpi).toHaveAttribute('aria-pressed', 'false');
});

test('RetroItemPhone_WhenMoreOpened_ShouldOfferTheRailSectionsAsSheets', async ({
  mount,
  page,
}) => {
  await serve(page);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  await page.getByTestId(touchTestIds.moreButton).tap();
  await page.getByTestId(touchTestIds.actionSheetRow).filter({ hasText: 'Written by' }).tap();
  await expect(page.getByTestId(ids.retroItemSheetWriters)).toBeVisible();
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Written by');
  await page.getByTestId(touchTestIds.sheetClose).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

test('RetroItemPhone_WhenChildRowTapped_ShouldBeOneLinkToTheChildRetro', async ({
  mount,
  page,
}) => {
  await serve(page);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  const [child] = retroItem.children;
  const row = page.getByTestId(ids.retroItemChildRow).first();
  await expect(row).toHaveAttribute('href', `/p/SHIP/retros/items/${child.item.key}`);
  expect((await row.boundingBox())!.height).toBeGreaterThanOrEqual(32);
});

test('RetroItemPhone_WhenActionItemsSheetOpened_ShouldGiveEachActionKeyATouchTarget', async ({
  mount,
  page,
}) => {
  await serve(page);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  await page.getByTestId(ids.retroItemKpiActions).tap();
  const links = page.getByTestId(ids.retroItemSheetActions).locator('a');
  await expect(links.first()).toBeVisible();
  for (const link of await links.all()) {
    expect((await link.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }
});

test('RetroItemPhone_WhenItemHasAParent_ShouldGivePartOfATouchTarget', async ({ mount, page }) => {
  await stubRetros(page);
  const parent = { ...retroItem.item, id: 'p-parent', key: 'SHIP-90', title: 'Checkout epic' };
  await page.route(/\/api\/projects\/[^/]+\/retros\/items\/[^/]+$/, (r) =>
    r.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ ...retroItem, parent }),
    }),
  );
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  const link = page.getByTestId(ids.retroItemPartOf);
  await expect(link).toHaveAttribute('href', '/p/SHIP/retros/items/SHIP-90');
  expect((await link.boundingBox())!.height).toBeGreaterThanOrEqual(32);
});

test('RetroItemDesktop_WhenOpenedAbove900_ShouldNotBreakTitlesMidWord', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await serve(page);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  const title = page.getByRole('heading', { level: 1 });
  await expect(title).toHaveCSS('overflow-wrap', 'normal');
  await expect(title).toHaveCSS('min-width', 'auto');
});
