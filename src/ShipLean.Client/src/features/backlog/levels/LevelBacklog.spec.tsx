import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { EPIC_ROWS, FEATURE_ROWS, levelView } from '../ct/levelFixtures';
import { backlogView, ROWS } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { LevelHarness } from '../ct/LevelHarness';
import { LevelSlotHarness } from '../ct/LevelSlotHarness';
import { PROJECT } from '../ct/fixtures';
import { stubLevel } from '../ct/stubLevels';
import { backlogTestIds as ids } from '../testIds';

const features = { hooksConfig: { route: '/p/SHIP/backlogs/features' } };
const epics = { hooksConfig: { route: '/p/SHIP/backlogs/epics' } };
const row = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.row}"][data-key="${key}"]`);
const headers = (page: {
  getByTestId(id: string): { evaluateAll<T>(f: (e: Element[]) => T): Promise<T> };
}) =>
  page
    .getByTestId(ids.columnHeader)
    .evaluateAll((els) => els.map((e) => e.getAttribute('data-column')));

test('Levels_WhenFeaturesLoaded_ShouldShowRowsWithProgressFromTheRollups', async ({
  mount,
  page,
}) => {
  const server = await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  expect(server.reads[0]).toContain('/backlogs/features?');
  expect(server.reads[0]).toContain('includeCompleted=false');
  expect(await headers(page)).toEqual([
    'rank',
    'key',
    'title',
    'parent',
    'state',
    'points',
    'progress',
  ]);
  const checkout = row(page, 'SHIP-101');
  await expect(checkout.getByTestId(ids.rank)).toHaveText('1');
  await expect(checkout.getByTestId(ids.levelProgressText)).toHaveText('4 / 8 done');
  await expect(checkout.getByTestId(ids.levelProgressDone)).toHaveAttribute('style', /width: 50%/);
  // Stories under the nested Feature count for its parent Feature.
  await expect(row(page, 'SHIP-103').getByTestId(ids.levelProgressText)).toHaveText('0 / 4 done');
  await expect(row(page, 'SHIP-108').getByTestId(ids.levelProgressText)).toHaveText('0 / 0 done');
  await expect(row(page, 'SHIP-108').getByTestId(ids.levelNested)).toBeVisible();
  await expect(checkout.getByTestId(ids.levelNested)).toHaveCount(0);
});

test('Levels_WhenSumExceedsOwn_ShouldGreyTheSumAndWarnOnTheServersSay', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  const warned = row(page, 'SHIP-101').getByTestId(ids.levelPoints);
  await expect(warned).toContainText('20');
  await expect(warned.getByTestId(ids.levelPointsSum)).toHaveText('· Σ26');
  await expect(warned.getByTestId(ids.levelPointsSum)).toHaveAttribute('data-warn', 'true');
  const calm = row(page, 'SHIP-103').getByTestId(ids.levelPointsSum);
  await expect(calm).toHaveText('· Σ13');
  await expect(calm).toHaveAttribute('data-warn', 'false');
  const unestimated = row(page, 'SHIP-108').getByTestId(ids.levelPoints);
  await expect(unestimated).toHaveText('?');
  await expect(unestimated.getByTestId(ids.levelPointsSum)).toHaveCount(0);
});

test('Levels_WhenEpicsLoaded_ShouldShowTheFeaturesCountInPlaceOfParent', async ({
  mount,
  page,
}) => {
  const server = await stubLevel(page, levelView('epics', EPIC_ROWS));
  await mount(<LevelHarness project={PROJECT} level="epics" />, epics);

  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  expect(server.reads[0]).toContain('/backlogs/epics?');
  expect(await headers(page)).toEqual([
    'rank',
    'key',
    'title',
    'state',
    'features',
    'points',
    'progress',
  ]);
  await expect(page.getByTestId(ids.levelFeatureCount)).toHaveText('2');
  await expect(page.getByTestId(ids.levelProgressText)).toHaveText('0 / 4 done');
});

test('Levels_WhenOnEachLevel_ShouldPressItsOptionAndNavigateOnPick', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  const options = page.getByTestId(ids.levelOption);
  await expect(options).toHaveText(['Epics', 'Features', 'Stories']);
  await expect(options.nth(1)).toHaveAttribute('aria-pressed', 'true');
  await expect(options.nth(0)).toHaveAttribute('aria-pressed', 'false');
  await options.nth(0).click();
  await expect(page.getByTestId('harness-location')).toHaveText('/p/SHIP/backlogs/epics');
  await options.nth(2).click();
  await expect(page.getByTestId('harness-location')).toHaveText('/p/SHIP/backlogs/stories');
});

test('Levels_WhenColumnsSaved_ShouldHideThemPerLevelAndKeepKeyAndTitle', async ({
  mount,
  page,
}) => {
  await stubLevel(page, levelView('epics', EPIC_ROWS), {
    saved: {
      hiddenColumns: { epics: ['features', 'progress', 'key', 'rank'], features: ['points'] },
    },
  });
  await mount(<LevelHarness project={PROJECT} level="epics" />, epics);

  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  expect(await headers(page)).toEqual(['rank', 'key', 'title', 'state', 'points']);
});

test('Levels_WhenColumnToggled_ShouldSaveALevelColumnTheStoriesListDoesNotHave', async ({
  mount,
  page,
}) => {
  const server = await stubLevel(page, levelView('epics', EPIC_ROWS), {
    saved: { hiddenColumns: { stories: ['sprint'] } },
  });
  await mount(<LevelHarness project={PROJECT} level="epics" />, epics);
  await expect(page.getByTestId(ids.row)).toHaveCount(1);

  await page.getByTestId(ids.columnsButton).click();
  await page.getByTestId(ids.columnOption).and(page.locator('[data-column="features"]')).click();
  await expect(page.getByTestId(ids.levelFeatureCount)).toHaveCount(0);
  await expect.poll(() => server.puts.length).toBe(1);
  expect(server.puts[0]).toEqual({ hiddenColumns: { stories: ['sprint'], epics: ['features'] } });
});

test('Levels_WhenCompletedToggled_ShouldReadWithIncludeCompleted', async ({ mount, page }) => {
  const server = await stubLevel(page, levelView('features', FEATURE_ROWS));
  await mount(<LevelHarness project={PROJECT} level="features" />, features);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);

  await page.getByTestId(ids.completedToggle).check();
  await expect.poll(() => server.reads.at(-1)).toContain('includeCompleted=true');
});

test('Levels_WhenNewItemAdded_ShouldCreateTheLevelsOwnType', async ({ mount, page }) => {
  const server = await stubLevel(page, levelView('epics', EPIC_ROWS));
  await mount(<LevelHarness project={PROJECT} level="epics" />, epics);
  await expect(page.getByTestId(ids.row)).toHaveCount(1);

  await page.getByTestId(ids.newItem).click();
  await expect(page.getByTestId(ids.addType)).toHaveAttribute('data-type', 'epic');
  await page.getByTestId(ids.addTitle).fill('Growth');
  await page.getByTestId(ids.addTitle).press('Enter');
  await expect.poll(() => server.creates.length).toBe(1);
  expect(server.creates[0]).toMatchObject({ type: 'epic', title: 'Growth', parentId: null });
  await expect(page.getByTestId(ids.addRow)).toBeVisible();
});

test('Levels_WhenFilterOn_ShouldSendItAndKeepRowsWhenRefused', async ({ mount, page }) => {
  const server = await stubLevel(page, levelView('features', FEATURE_ROWS), {
    answer: (search) =>
      search.get('filter')?.includes('Bad')
        ? { status: 400, body: { error: 'The filter is not valid criteria.', code: 'validation' } }
        : undefined,
  });
  await mount(<LevelHarness project={PROJECT} level="features" />, features);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);

  await page.getByTestId(ids.filterButton).click();
  await expect(page.getByTestId(ids.filterPanel)).toBeVisible();
  const clause = page.getByTestId(ids.filterPanel).getByRole('textbox').first();
  await clause.fill('Bad');
  await expect.poll(() => server.reads.at(-1)).toContain(encodeURIComponent('Bad'));
  await expect(page.getByTestId(ids.filterError)).toBeVisible();
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
});

test('Levels_WhenLoadFails_ShouldSayItCouldNotLoad', async ({ mount, page }) => {
  await stubLevel(page, levelView('features', FEATURE_ROWS), {
    answer: () => ({ status: 500, body: { error: 'x', code: 'internal_error' } }),
  });
  await mount(<LevelHarness project={PROJECT} level="features" />, features);

  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
});

test('Levels_WhenLevelSwitchedByTheAddress_ShouldStartFreshAndShowNoOldRows', async ({
  mount,
  page,
}) => {
  const reads: string[] = [];
  let release: () => void = () => undefined;
  const epicsHeld = new Promise<void>((resolve) => (release = resolve));
  await page.route('**/api/projects/p1/backlogs/*?*', async (route) => {
    const url = new URL(route.request().url());
    reads.push(url.pathname + url.search);
    const isEpics = url.pathname.endsWith('/epics');
    if (isEpics) await epicsHeld;
    const body = isEpics ? levelView('epics', EPIC_ROWS) : levelView('features', FEATURE_ROWS);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(body),
    });
  });
  for (const list of ['members', 'states', 'sprints']) {
    await page.route(`**/api/projects/p1/${list}`, (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }),
    );
  }
  await page.route('**/api/me/preferences/**', (route) => route.fulfill({ status: 204 }));
  await mount(<LevelSlotHarness project={PROJECT} />, features);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await page.getByTestId(ids.completedToggle).check();
  await expect.poll(() => reads.at(-1)).toContain('includeCompleted=true');

  await page.locator(`[data-testid="${ids.levelOption}"][data-level="epics"]`).click();
  await expect.poll(() => reads.some((read) => read.includes('/epics'))).toBe(true);

  // The new level reads with its own defaults and shows nothing of the old one while it loads.
  expect(reads.filter((read) => read.includes('/epics'))).toEqual([
    expect.stringContaining('includeCompleted=false'),
  ]);
  await expect(page.getByTestId(ids.loading)).toBeVisible();
  await expect(page.getByTestId(ids.row)).toHaveCount(0);
  release();
  await expect(page.getByTestId(ids.row)).toHaveCount(EPIC_ROWS.length);
  await expect(page.getByTestId(ids.completedToggle)).not.toBeChecked();
  expect(await headers(page)).toContain('features');
});

test('Levels_WhenStoriesBacklogShown_ShouldOfferThePickerWithStoriesPressed', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });

  const option = (level: string) =>
    page.locator(`[data-testid="${ids.levelOption}"][data-level="${level}"]`);
  await expect(option('stories')).toHaveAttribute('aria-pressed', 'true');
  await expect(option('features')).toHaveAttribute('aria-pressed', 'false');
  await expect(option('epics')).toHaveAttribute('aria-pressed', 'false');
});
