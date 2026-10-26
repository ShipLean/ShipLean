import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../../components/testIds';
import {
  backlogView,
  EMPTY_PROJECT,
  itemRow,
  planningSprints,
  PROJECT,
  ROWS,
} from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';
import { ListMode } from './ListMode';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const headers = (page: {
  getByTestId(id: string): { evaluateAll<T>(f: (e: Element[]) => T): Promise<T> };
}) =>
  page
    .getByTestId(ids.columnHeader)
    .evaluateAll((els) => els.map((e) => e.getAttribute('data-column')));

test('List_WhenLoaded_ShouldShowRowsInRankOrderWithCells', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);

  const rows = page.getByTestId(ids.row);
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toHaveAttribute('data-key', 'SHIP-160');
  await expect(rows.nth(0).getByTestId(ids.rank)).toHaveText('3');
  await expect(rows.nth(1).getByTestId(ids.rank)).toHaveText('7');
  await expect(rows.nth(0).getByTestId(ids.title)).toHaveText('Biometric sign-in');
  await expect(rows.nth(0).getByTestId(ids.title)).toHaveAttribute(
    'href',
    '/p/SHIP/items/SHIP-160',
  );
  await expect(rows.nth(0).getByTestId(ids.parent)).toContainText('Mobile sign-in');
  await expect(rows.nth(0).getByTestId(ids.points)).toHaveText('?');
  await expect(rows.nth(1).getByTestId(ids.points)).toHaveText('5');
  await expect(rows.nth(0).getByTestId(componentTestIds.avatar)).toHaveAttribute(
    'data-variant',
    'person',
  );
  await expect(rows.nth(1).getByTestId(componentTestIds.avatar)).toHaveAttribute(
    'data-variant',
    'none',
  );
});

test('List_WhenRowsHaveSprints_ShouldShowChipsAndAccentTheCurrentOne', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  const chips = page.getByTestId(ids.sprintChip);
  await expect(chips).toHaveText(['Sprint 24', 'Sprint 25']);
  await expect(chips.nth(0).getByTestId(componentTestIds.chip)).toHaveAttribute(
    'data-tone',
    'accent',
  );
  await expect(chips.nth(1).getByTestId(componentTestIds.chip)).toHaveAttribute(
    'data-tone',
    'muted',
  );
});

test('List_WhenRowHasTags_ShouldColourThemByTheBoard', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  const tags = page.getByTestId(ids.tag);
  await expect(tags).toHaveText(['security', 'spike', 'misc']);
  await expect(tags.nth(0).getByTestId(componentTestIds.chip)).toHaveAttribute(
    'data-tone',
    'danger',
  );
  await expect(tags.nth(1).getByTestId(componentTestIds.chip)).toHaveAttribute(
    'data-tone',
    'purple',
  );
  await expect(tags.nth(2).getByTestId(componentTestIds.chip)).toHaveAttribute(
    'data-tone',
    'muted',
  );
});

test('List_WhenColumnsSaved_ShouldHideThemAndKeepKeyAndTitle', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS), {
    saved: { hiddenColumns: { stories: ['sprint', 'tags', 'key', 'title', 'rank'] } },
  });
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  expect(await headers(page)).toEqual([
    'rank',
    'key',
    'title',
    'parent',
    'state',
    'points',
    'assignee',
  ]);
  await expect(page.getByTestId(ids.sprintChip)).toHaveCount(0);
  await expect(page.getByTestId(ids.tag)).toHaveCount(0);
});

test('List_WhenColumnToggled_ShouldHideItAndSaveThePreference', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(2);

  await page.getByTestId(ids.columnsButton).click();
  await page.getByTestId(ids.columnOption).and(page.locator('[data-column="parent"]')).click();
  await expect(page.getByTestId(ids.parent)).toHaveCount(0);
  await expect.poll(() => server.puts.length).toBe(1);
  expect(server.puts[0]).toEqual({ hiddenColumns: { stories: ['parent'] } });

  await page.getByTestId(ids.columnOption).and(page.locator('[data-column="parent"]')).click();
  await expect(page.getByTestId(ids.parent)).toHaveCount(1);
  await expect.poll(() => server.puts.length).toBe(2);
  expect(server.puts[1]).toEqual({ hiddenColumns: { stories: [] } });
});

test('List_WhenCompletedToggled_ShouldReadWithIncludeCompleted', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  expect(server.reads[0]).toContain('includeCompleted=false');

  await page.getByTestId(ids.completedToggle).check();
  await expect.poll(() => server.reads.at(-1)).toContain('includeCompleted=true');
  expect(server.reads.at(-1)).toContain('mode=list');
});

test('List_WhenNewItemAdded_ShouldPostTypeAndTitleAndKeepRowOpen', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);

  await page.getByTestId(ids.newItem).click();
  await expect(page.getByTestId(ids.addTitle)).toBeFocused();
  await page.getByTestId(ids.addType).selectOption('bug');
  await page.getByTestId(ids.addTitle).fill('Crash on launch');
  server.setBacklog(
    backlogView([itemRow({ key: 'SHIP-184', title: 'Crash on launch', rank: 1 }), ...ROWS]),
  );
  await page.getByTestId(ids.addTitle).press('Enter');

  await expect(page.getByTestId(ids.row).first()).toHaveAttribute('data-key', 'SHIP-184');
  expect(server.creates).toEqual([
    {
      projectId: 'p1',
      type: 'bug',
      title: 'Crash on launch',
      parentId: null,
      sprintId: null,
      changes: null,
    },
  ]);
  await expect(page.getByTestId(ids.addTitle)).toHaveValue('');
  await expect(page.getByTestId(ids.addRow)).toBeVisible();

  await page.getByTestId(ids.addTitle).press('Escape');
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);
});

test('List_WhenTitleBlank_ShouldNotCreate', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await page.getByTestId(ids.newItem).click();
  await page.getByTestId(ids.addTitle).fill('   ');
  await page.getByTestId(ids.addSubmit).click();
  expect(server.creates).toEqual([]);
});

test('List_WhenCreateRefused_ShouldShowTheErrorAndKeepTheTitle', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS), {
    createStatus: 400,
    createBody: { code: 'validation', error: 'Title is too long.' },
  });
  await mount(<ListMode project={PROJECT} />, route);
  await page.getByTestId(ids.newItem).click();
  await page.getByTestId(ids.addTitle).fill('x');
  await page.getByTestId(ids.addSubmit).click();
  await expect(page.getByTestId(ids.addError)).toHaveText('Title is too long.');
  await expect(page.getByTestId(ids.addTitle)).toHaveValue('x');
});

test('List_WhenProjectEmpty_ShouldOfferFirstStoryAndImport', async ({ mount, page }) => {
  await stubBacklog(page, backlogView([]));
  await mount(<ListMode project={EMPTY_PROJECT} />, route);
  await expect(page.getByTestId(ids.empty)).toBeVisible();
  await expect(page.getByTestId(ids.emptyImport)).toHaveAttribute('href', '/p/SHIP/import');
  await expect(page.getByTestId(ids.emptySprint)).toHaveAttribute('href', '/p/SHIP/sprints');
  await expect(page.getByTestId(ids.list)).toHaveCount(0);

  await page.getByTestId(ids.emptyCreate).click();
  await expect(page.getByTestId(ids.empty)).toHaveCount(0);
  await expect(page.getByTestId(ids.addRow)).toBeVisible();
});

test('List_WhenProjectEmptyAndHasASprint_ShouldNotSayThereAreNoSprints', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, backlogView([]), { sprints: planningSprints() });
  await mount(<ListMode project={EMPTY_PROJECT} />, route);
  await expect(page.getByTestId(ids.empty)).toBeVisible();
  await expect(page.getByTestId(ids.emptyImport)).toBeVisible();
  await expect(page.getByTestId(ids.emptySprint)).toHaveCount(0);
  await expect(page.getByTestId(ids.empty)).not.toContainText('No sprints yet');
});

test('List_WhenNoRowsButProjectHasItems_ShouldNotShowFirstRun', async ({ mount, page }) => {
  await stubBacklog(page, backlogView([]));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.noRows)).toBeVisible();
  await expect(page.getByTestId(ids.empty)).toHaveCount(0);
});

test('List_WhenBacklogReadFails_ShouldShowLoadFailed', async ({ mount, page }) => {
  await page.route('**/api/projects/p1/backlog?*', (route) =>
    route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: '{"code":"internal_error","error":"x"}',
    }),
  );
  await page.route('**/api/me/preferences/**', (route) => route.fulfill({ status: 204 }));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
});
