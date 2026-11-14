import { expect, test } from '@playwright/experimental-ct-react';
import { FEATURE_TREE_ROWS, levelView } from '../ct/levelFixtures';
import { LevelSlotHarness } from '../ct/LevelSlotHarness';
import { PROJECT } from '../ct/fixtures';
import { stubLevel } from '../ct/stubLevels';
import { backlogTestIds as ids } from '../testIds';

// The Features tree at 900px and below (SHIP-716 D6, ruling R5): TouchLevelRow rows under path headers, none indented.
// Runs in the CT phone project.

const featuresTree = { hooksConfig: { route: '/p/SHIP/backlogs/features?mode=tree' } };

test('LevelTree_WhenTouch_ShouldRenderTouchRowsNotTable', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_TREE_ROWS, 'tree'));
  await mount(<LevelSlotHarness project={PROJECT} />, featuresTree);

  await expect(page.getByTestId(ids.row)).toHaveCount(4);
  await expect(page.getByTestId(ids.treeGroup)).toHaveCount(3);
  await expect(page.getByTestId(ids.levelTouchProgress)).toHaveCount(4);
  await expect(page.locator('table')).toHaveCount(0);
  await expect(page.getByTestId(ids.columnHeader)).toHaveCount(0);
  const sideways = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
  expect(sideways).toBe(false);
  // The two Features with Features under them (SHIP-103) fold with a caret of their own, at least 32px.
  const caret = await page
    .locator('[data-testid="' + ids.treeCaret + '"][aria-label="Collapse Reporting"]')
    .boundingBox();
  expect(caret!.width).toBeGreaterThanOrEqual(32);
  expect(caret!.height).toBeGreaterThanOrEqual(32);
});

test('LevelTree_WhenTouch_ShouldNotIndentAndShowPath', async ({ mount, page }) => {
  // Reporting (SHIP-103) is a group here, so its header names the Epic above it.
  const rows = FEATURE_TREE_ROWS.map((row) =>
    row.key === 'SHIP-103' ? { ...row, kind: 'group' as const } : row,
  );
  await stubLevel(page, levelView('features', rows, 'tree'));
  await mount(<LevelSlotHarness project={PROJECT} />, featuresTree);

  const header = (key: string) =>
    page.locator(`[data-testid="${ids.treeGroup}"][data-key="${key}"]`);
  await expect(header('SHIP-92').getByTestId(ids.treeGroupPath)).toHaveText(
    'Insights for merchants',
  );
  await expect(header('SHIP-103').getByTestId(ids.treeGroupPath)).toHaveText(
    'Insights for merchants › Reporting',
  );
  await expect(page.getByTestId(ids.treeGroupPath).last()).toHaveText('No parent');
  // SHIP-108 (depth 2) and SHIP-101 (depth 1) start at the same left edge.
  const left = (key: string) =>
    page
      .locator(`[data-testid="${ids.row}"][data-key="${key}"]`)
      .evaluate((el) => el.getBoundingClientRect().left);
  expect(await left('SHIP-108')).toBe(await left('SHIP-101'));
});
