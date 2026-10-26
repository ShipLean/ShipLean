import { expect, test } from '@playwright/experimental-ct-react';
import { criteriaTestIds } from '../../criteria/testIds';
import { backlogView, itemRow, PROJECT, ROWS } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';
import { ListMode } from '../list/ListMode';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const filterOf = (search: string) => new URLSearchParams(search).get('filter');

test('Filter_WhenOpened_ShouldSendTheCriteriaAsTheFilterParam', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  expect(filterOf(server.reads[0])).toBe('');

  await page.getByTestId(ids.filterButton).click();
  await expect(page.getByTestId(ids.filterPanel)).toBeVisible();
  await expect(page.getByTestId(ids.filterButton)).toHaveText('Filter (1)');
  await expect.poll(() => filterOf(server.reads.at(-1) ?? '')).not.toBe('');
  expect(JSON.parse(filterOf(server.reads.at(-1) ?? '') ?? '')).toEqual({
    match: 'all',
    clauses: [{ field: 'type', op: '=', value: expect.any(String) }],
  });
});

test('Filter_WhenPanelOpened_ShouldRenderBelowTheToolbarNotInsideIt', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await page.getByTestId(ids.filterButton).click();
  await expect(page.getByTestId(ids.toolbar).getByTestId(ids.filterPanel)).toHaveCount(0);
  await expect(
    page.getByTestId(ids.filterPanel).getByTestId(criteriaTestIds.builder),
  ).toBeVisible();
});

test('Filter_WhenServerRefuses_ShouldShowItsMessageAndKeepTheRows', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS), {
    answer: (search) =>
      search.get('filter')
        ? { status: 400, body: { error: 'Filter clause 1 is not valid.', code: 'validation' } }
        : undefined,
  });
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(2);

  await page.getByTestId(ids.filterButton).click();
  await expect(page.getByTestId(ids.filterError)).toHaveText('Filter clause 1 is not valid.');
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  await expect(page.getByTestId(ids.loadFailed)).toHaveCount(0);
});

test('Filter_WhenCleared_ShouldReadWithoutAFilterAndEnableDrag', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await page.getByTestId(ids.filterButton).click();
  await expect(page.getByTestId(ids.dragOffBanner)).toBeVisible();

  await page.getByTestId(ids.filterClear).click();
  await expect(page.getByTestId(ids.dragOffBanner)).toHaveCount(0);
  await expect(page.getByTestId(ids.filterButton)).toHaveText('Filter');
  expect(filterOf(server.reads.at(-1) ?? '')).toBe('');
});

test('Filter_WhenOn_ShouldDisableDragWithBannerAndKeepFullBacklogRank', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.grip).first()).toBeEnabled();
  await expect(page.getByTestId(ids.dragOffBanner)).toHaveCount(0);

  await page.getByTestId(ids.filterButton).click();
  await expect(page.getByTestId(ids.dragOffBanner)).toContainText('Reordering is off');
  await expect(page.getByTestId(ids.grip).first()).toBeDisabled();
  await expect(page.getByTestId(ids.row).nth(1).getByTestId(ids.rank)).toHaveText('7');
});

test('Filter_WhenChipRemoved_ShouldDropTheClause', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView([itemRow({ key: 'SHIP-1', rank: 1 })]));
  await mount(<ListMode project={PROJECT} />, route);
  await page.getByTestId(ids.filterButton).click();
  await expect(page.getByTestId(ids.filterSlot).getByTestId(criteriaTestIds.chips)).toBeVisible();
  await page.getByTestId(ids.filterSlot).getByTestId('chip-remove').click();
  await expect(page.getByTestId(ids.filterButton)).toHaveText('Filter');
  expect(filterOf(server.reads.at(-1) ?? '')).toBe('');
});

test('Filter_WhenAClauseIsHalfTyped_ShouldKeepTheLastWholeFilterDragOffAndBannerOn', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await page.getByTestId(ids.filterButton).click();
  await expect(page.getByTestId(ids.dragOffBanner)).toBeVisible();
  await page.getByTestId(criteriaTestIds.add).click();
  await expect(page.getByTestId(ids.filterButton)).toHaveText('Filter (2)');
  await expect
    .poll(() => JSON.parse(filterOf(server.reads.at(-1) ?? '') || '{}').clauses?.length)
    .toBe(2);
  const whole = filterOf(server.reads.at(-1) ?? '');
  const readsBefore = server.reads.length;

  await page.getByTestId(criteriaTestIds.field).nth(1).selectOption('points');
  await expect(page.getByTestId(criteriaTestIds.error)).toBeVisible();

  await expect(page.getByTestId(ids.dragOffBanner)).toBeVisible();
  await expect(page.getByTestId(ids.grip).first()).toBeDisabled();
  await expect(page.getByTestId(ids.filterButton)).toHaveText('Filter (2)');
  expect(
    server.reads
      .slice(readsBefore)
      .map(filterOf)
      .every((f) => f === whole),
  ).toBe(true);
  expect(filterOf(server.reads.at(-1) ?? '')).toBe(whole);
});

test('Filter_WhenNothingMatches_ShouldSayNothingMatchesThisFilter', async ({ mount, page }) => {
  await stubBacklog(page, backlogView([]), {
    answer: (search) => (search.get('filter') ? { status: 200, body: backlogView([]) } : undefined),
  });
  await mount(<ListMode project={PROJECT} />, route);
  await page.getByTestId(ids.filterButton).click();
  await expect(page.getByTestId(ids.noRows)).toHaveText('Nothing matches this filter');
});
