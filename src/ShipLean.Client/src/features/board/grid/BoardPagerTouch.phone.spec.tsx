import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { GridHarness } from '../ct/GridHarness';
import { featuresBoard, storiesBoard } from '../ct/fixtures';
import { boardTestIds as ids } from '../testIds';

// The Stories, Features and Epics boards' column pager on a phone (SHIP-719, L719a). Runs in the CT phone project.
// Element shots cover this task's regions only. Regenerate with
// scripts/update-page-shots.sh src/features/board/grid/BoardPagerTouch.phone.spec.tsx.

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
const THEMES = ['light', 'dark'] as const;

const chip = (page: Page, columnId: string) =>
  page.locator(`[data-testid="${touchTestIds.pagerChip}"][data-page-id="${columnId}"]`);
const pageOf = (page: Page, columnId: string) =>
  page.locator(`[data-testid="${touchTestIds.pagerPage}"][data-page-id="${columnId}"]`);

test('BoardPages_WhenOverWip_ShouldMarkChipDanger', async ({ mount, page }) => {
  await mount(<GridHarness view={storiesBoard()} />);

  await expect(page.getByTestId(touchTestIds.pagerChip)).toHaveCount(4);
  await expect(chip(page, 'col-prog')).toHaveAttribute('data-over', 'true');
  await expect(chip(page, 'col-prog')).toContainText('3/2');
  await expect(chip(page, 'col-ready')).toHaveAttribute('data-over', 'false');
  await expect(chip(page, 'col-ready')).toContainText('3/8');
  await expect(chip(page, 'col-new')).toHaveAttribute('aria-current', 'true');
  await expect(pageOf(page, 'col-prog').getByTestId(ids.columnHeader)).toHaveAttribute(
    'data-over',
    'true',
  );
});

test('BoardPages_WhenLaneToggled_ShouldCollapseInEveryColumn', async ({ mount, page }) => {
  await mount(<GridHarness view={storiesBoard()} />);
  const lanes = page.locator(`[data-testid="${ids.columnLane}"][data-lane="lane-default"]`);
  await expect(lanes).toHaveCount(3);

  await lanes.first().getByTestId(ids.columnLaneToggle).tap();

  for (let i = 0; i < 3; i++) {
    await expect(lanes.nth(i)).toHaveAttribute('data-collapsed', 'true');
    await expect(lanes.nth(i).getByTestId(ids.cell)).toHaveCount(0);
    await expect(lanes.nth(i).getByTestId(ids.columnLaneName)).toBeVisible();
  }
  // A lane with no cards in a column is left out of it; the other lane is unaffected.
  await expect(pageOf(page, 'col-prog').locator(`[data-lane="lane-default"]`)).toHaveCount(0);
  await expect(
    pageOf(page, 'col-prog').locator(`[data-lane="lane-expedite"]`).getByTestId(ids.cell),
  ).toHaveCount(1);
});

test('BoardPages_WhenTablet_ShouldShowTwoAndABitColumns', async ({ mount, page }) => {
  await page.setViewportSize({ width: 820, height: 1180 });
  await mount(<GridHarness view={storiesBoard()} />);

  const second = await pageOf(page, 'col-ready').boundingBox();
  const third = await pageOf(page, 'col-prog').boundingBox();
  expect(second!.width).toBe(340);
  expect(second!.x + second!.width).toBeLessThanOrEqual(820);
  expect(third!.x).toBeLessThan(820);
  expect(third!.x + third!.width).toBeGreaterThan(820);
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
      ),
    )
    .toBe(true);
});

for (const theme of THEMES) {
  test(`BoardPagerPhone_WhenOpened_ShouldMatchItsScreenshot_board-pager_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<GridHarness view={storiesBoard()} />);
    await expect(page.getByTestId(ids.pagerGrid)).toHaveScreenshot(`board-pager.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`BoardPagerPhone_WhenLanesPage_ShouldMatchItsScreenshot_features-board-lanes-page_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<GridHarness view={featuresBoard()} level="features" />);
    await expect(pageOf(page, 'col-new').getByTestId(ids.card)).toHaveCount(1);
    await expect(pageOf(page, 'col-new')).toHaveScreenshot(
      `features-board-lanes-page.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
