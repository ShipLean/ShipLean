import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { EPIC_ROWS, FEATURE_ROWS, FEATURE_TREE_ROWS, levelView } from '../ct/levelFixtures';
import { LevelSlotHarness } from '../ct/LevelSlotHarness';
import { PROJECT } from '../ct/fixtures';
import { stubLevel } from '../ct/stubLevels';
import { backlogTestIds as ids } from '../testIds';

const at = (route: string) => ({ hooksConfig: { route } });
const featuresList = at('/p/SHIP/backlogs/features');
const featuresTree = at('/p/SHIP/backlogs/features?mode=tree');
// Every Feature and group row in order, as "key:kind".
const shown = (page: Page) =>
  page
    .locator(`[data-testid="${ids.row}"], [data-testid="${ids.treeGroup}"]`)
    .evaluateAll((els) =>
      els.map((e) => `${e.getAttribute('data-key')}:${e.getAttribute('data-kind')}`),
    );
const option = (page: Page, mode: string) =>
  page.locator(`[data-testid="${ids.modeOption}"][data-mode="${mode}"]`);
const caretOf = (page: Page, key: string) =>
  page.locator(`[data-key="${key}"]`).getByTestId(ids.treeCaret);

test('FeaturesTree_WhenListOpen_ShouldOfferListAndTreeAndSwitchByTheAddress', async ({
  mount,
  page,
}) => {
  const server = await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelSlotHarness project={PROJECT} />, featuresList);

  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await expect(page.getByTestId(ids.modeOption)).toHaveText(['List', 'Tree']);
  await expect(option(page, 'list')).toHaveAttribute('aria-pressed', 'true');
  expect(server.reads[0]).not.toContain('mode=');

  await stubLevel(page, levelView('features', FEATURE_TREE_ROWS, 'tree'));
  await option(page, 'tree').click();

  await expect(page.getByTestId('harness-address')).toHaveText(
    '/p/SHIP/backlogs/features?mode=tree',
  );
  await expect(option(page, 'tree')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId(ids.treeGroup)).toHaveCount(3);
});

test('FeaturesTree_WhenOpenedWithModeTree_ShouldNestFeaturesUnderEpicsWithNoParentLast', async ({
  mount,
  page,
}) => {
  const server = await stubLevel(page, levelView('features', FEATURE_TREE_ROWS, 'tree'));
  await mount(<LevelSlotHarness project={PROJECT} />, featuresTree);

  await expect
    .poll(() => shown(page))
    .toEqual([
      'SHIP-92:group',
      'SHIP-103:item',
      'SHIP-108:item',
      'SHIP-90:group',
      'SHIP-101:item',
      ':noParentGroup',
      'SHIP-104:item',
    ]);
  expect(server.reads[0]).toContain('/backlogs/features?mode=tree&');
  await expect(option(page, 'tree')).toHaveAttribute('aria-pressed', 'true');
  // The nesting is the parent, so # and Parent are left out.
  await expect(page.getByTestId(ids.columnHeader)).toHaveText([
    'Key',
    'Title',
    'State',
    'Pts',
    'Progress (stories, bugs and Action items)',
  ]);
  const noParent = page.locator(`[data-testid="${ids.treeGroup}"][data-kind="noParentGroup"]`);
  await expect(noParent.getByTestId(ids.treeRollup)).toHaveText('1 Feature');
  // Group rows keep the level's cells: the Epic's own progress shows.
  await expect(page.locator('[data-key="SHIP-92"]').getByTestId(ids.levelProgressText)).toHaveText(
    '0 / 4 done',
  );
  // Only rows with rows under them fold.
  await expect(caretOf(page, 'SHIP-103')).toBeVisible();
  await expect(caretOf(page, 'SHIP-108')).toHaveCount(0);
  await expect(caretOf(page, 'SHIP-101')).toHaveCount(0);
});

test('FeaturesTree_WhenBranchesFolded_ShouldHideWhatIsUnderThemOnly', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_TREE_ROWS, 'tree'));
  await mount(<LevelSlotHarness project={PROJECT} />, featuresTree);
  await expect(page.getByTestId(ids.row)).toHaveCount(4);

  await caretOf(page, 'SHIP-103').click();
  await expect(caretOf(page, 'SHIP-103')).toHaveAttribute('aria-expanded', 'false');
  await expect.poll(() => shown(page)).not.toContain('SHIP-108:item');
  expect(await shown(page)).toContain('SHIP-103:item');

  await caretOf(page, 'SHIP-92').click();
  await expect
    .poll(() => shown(page))
    .toEqual([
      'SHIP-92:group',
      'SHIP-90:group',
      'SHIP-101:item',
      ':noParentGroup',
      'SHIP-104:item',
    ]);

  await caretOf(page, 'SHIP-92').click();
  // The Feature folded inside the Epic stays folded when the Epic opens again.
  await expect.poll(() => shown(page)).toContain('SHIP-103:item');
  expect(await shown(page)).not.toContain('SHIP-108:item');
});

test('FeaturesTree_WhenEpicsOpenWithModeTree_ShouldShowTheListWithNoModeSwitch', async ({
  mount,
  page,
}) => {
  const server = await stubLevel(page, levelView('epics', EPIC_ROWS));
  await mount(<LevelSlotHarness project={PROJECT} />, at('/p/SHIP/backlogs/epics?mode=tree'));

  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  await expect(page.getByTestId(ids.modeSwitch)).toHaveCount(0);
  expect(server.reads[0]).not.toContain('mode=');
});
