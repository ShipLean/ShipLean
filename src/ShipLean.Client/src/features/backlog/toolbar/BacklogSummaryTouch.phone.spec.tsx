import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { backlogView, itemRow, PROJECT, ROWS, treeRows, treeView } from '../ct/fixtures';
import { EPIC_ROWS, FEATURE_ROWS, levelView } from '../ct/levelFixtures';
import { LevelHarness } from '../ct/LevelHarness';
import { stubBacklog } from '../ct/stubApi';
import { stubLevel } from '../ct/stubLevels';
import { criteriaTestIds } from '../../criteria/testIds';
import { ListMode } from '../list/ListMode';
import { backlogTestIds as ids } from '../testIds';
import { TreeMode } from '../tree/TreeMode';

// The summary line under the touch toolbar, the filter sheet's live count and the ⋯ More toggle (SHIP-1821).
// Runs in the CT phone project.

const stories = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const open = (key: string, points: number) => itemRow({ key, points });
const done = { id: 'd', name: 'Done', category: 'completed' } as const;
const toggle = (id: string) =>
  `[data-testid="${touchTestIds.actionSheetToggle}"][data-action-id="${id}"]`;
const stubWhole = (rows: ReturnType<typeof open>[]) =>
  ({
    answer: (search: URLSearchParams) =>
      search.get('filter') ? { status: 200, body: backlogView(rows.slice(0, 2)) } : undefined,
  }) as const;

test('BacklogSummary_ShouldShowCountAndOpenPoints_OnStoriesList', async ({ mount, page }) => {
  const rows = [open('SHIP-1', 5), open('SHIP-2', 8), itemRow({ key: 'SHIP-3', points: 13, state: done })];
  await stubBacklog(page, backlogView(rows));
  await mount(<ListMode project={PROJECT} />, stories);
  await expect(page.getByTestId(ids.summary)).toContainText('3 items');
  await expect(page.getByTestId(ids.summary)).toContainText('13 pts open');
  await expect(page.getByTestId(ids.summaryNote)).toHaveText('Completed hidden');
});

test('BacklogSummary_ShouldShowCountAndOpenPoints_OnStoriesTree', async ({ mount, page }) => {
  const { rows } = treeRows();
  await stubBacklog(page, treeView(rows));
  await mount(<TreeMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=tree' },
  });
  const items = rows.filter((row) => row.kind === 'item');
  await expect(page.getByTestId(ids.summary)).toContainText(`${items.length} item`);
  await expect(page.getByTestId(ids.summary)).toContainText('pts open');
});

test('BacklogSummary_ShouldShowCountAndOpenPoints_OnFeatures', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/features' },
  });
  await expect(page.getByTestId(ids.summary)).toContainText(`${FEATURE_ROWS.length} Features`);
  await expect(page.getByTestId(ids.summaryNote)).toHaveText('Completed hidden');
});

test('BacklogSummary_ShouldShowCountAndOpenPoints_OnEpics', async ({ mount, page }) => {
  await stubLevel(page, levelView('epics', EPIC_ROWS));
  await mount(<LevelHarness project={PROJECT} level="epics" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/epics' },
  });
  await expect(page.getByTestId(ids.summary)).toContainText(`${EPIC_ROWS.length} Epics`);
});

test('BacklogSummary_WhenFiltered_ShouldSayShowingNOfM', async ({ mount, page }) => {
  const rows = [open('SHIP-1', 5), open('SHIP-2', 8), open('SHIP-3', 3), open('SHIP-4', 1)];
  await stubBacklog(page, backlogView(rows), stubWhole(rows));
  await mount(<ListMode project={PROJECT} />, stories);
  await expect(page.getByTestId(ids.row)).toHaveCount(4);
  await page.getByTestId(ids.filterButton).tap();
  await page.getByTestId(criteriaTestIds.add).tap();
  await expect(page.getByTestId(ids.summaryNote)).toHaveText('Showing 2 of 4');
  // The same two numbers in the filter sheet's footer.
  await expect(page.getByTestId(ids.filterCount)).toContainText('Showing 2 of 4');
});

test('BacklogMoreSheet_ShowCompleted_ShouldToggleWithoutClosing', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, stories);
  await page.getByTestId(ids.toolbarRow).getByTestId(touchTestIds.moreButton).tap();
  const row = page.locator(toggle('completed'));
  await expect(row).toHaveAttribute('aria-checked', 'false');
  await row.tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
  await expect(row).toHaveAttribute('aria-checked', 'true');
  await expect(page.getByTestId(ids.summaryNote)).toHaveCount(0);
});

test('BacklogSearch_AtTouch_ShouldNotShowTheReorderingOffBanner', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, stories);
  await page.getByTestId(ids.toolbarRow).getByTestId(touchTestIds.moreButton).tap();
  await page
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="search"]`)
    .tap();
  await page.getByTestId(ids.searchBox).fill(ROWS[0].key);
  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  await expect(page.getByTestId(ids.dragOffBanner)).toHaveCount(0);
  await expect(page.getByTestId(ids.searchCancel)).toHaveText('Done');
});

test('EpicsBacklog_NewButton_ShouldSayNewEpic', async ({ mount, page }) => {
  await stubLevel(page, levelView('epics', EPIC_ROWS));
  await mount(<LevelHarness project={PROJECT} level="epics" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/epics' },
  });
  await expect(page.getByTestId(ids.newItem)).toHaveText('+ New Epic');
});
