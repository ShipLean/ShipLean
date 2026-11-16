import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { BoardViewDto } from '@shiplean/core/api/views';
import { BoardHarness } from '../ct/BoardHarness';
import { COLUMNS, TOM, column, featuresBoard, storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { featuresWithHealth, filteredByHealth } from './ct/healthBoard';
import { boardTestIds as ids } from '../testIds';

const stories = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const features = { hooksConfig: { route: '/p/SHIP/boards/features' } };
const MEI = { ...TOM, id: 'u-mei', name: 'Mei Tanaka', initials: 'MT' };

// While any filter is set the server hides three cards, and says so on the first column.
function filtered(base: BoardViewDto, search: URLSearchParams): BoardViewDto {
  const on = [...search.keys()].some((name) => name !== 'older');
  return on
    ? {
        ...base,
        columns: base.columns.map((candidate, index) =>
          index === 0 ? column({ ...candidate, hiddenCount: 3 }) : candidate,
        ),
      }
    : base;
}

test('BoardFilter_WhenKeywordTyped_ShouldAskTheServerOnceTypingPauses', async ({ mount, page }) => {
  const server = await stubBoard(page, (search) => filtered(storiesBoard(), search));
  await mount(<BoardHarness />, stories);
  await page.getByTestId(ids.filterToggle).click();

  await page.getByTestId(ids.filterKeyword).fill('payout');

  await expect.poll(() => server.reads.at(-1)).toBe('?q=payout');
  await expect(page.getByTestId(ids.filterToggle)).toHaveText('Filter (1)');
});

test('BoardFilter_WhenPeopleAndUnassignedPicked_ShouldSendEachAsAssignee', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, (search) => filtered(storiesBoard(), search), {
    members: [TOM, MEI],
  });
  await mount(<BoardHarness />, stories);
  await page.getByTestId(ids.filterToggle).click();

  await page.getByTestId(ids.filterPerson).filter({ hasText: 'Tom Okafor' }).click();
  await page.getByTestId(ids.filterPerson).filter({ hasText: 'Unassigned' }).click();

  await expect.poll(() => server.reads.at(-1)).toBe('?assignee=u-tom&assignee=none');
  await page.getByTestId(ids.filterPerson).filter({ hasText: 'Tom Okafor' }).click();
  await expect.poll(() => server.reads.at(-1)).toBe('?assignee=none');
});

test('BoardFilter_WhenSprintPicked_ShouldSendTheSprint', async ({ mount, page }) => {
  const server = await stubBoard(page, (search) => filtered(storiesBoard(), search), {
    sprints: [{ id: 's25', name: 'Sprint 25' }],
  });
  await mount(<BoardHarness />, stories);
  await page.getByTestId(ids.filterToggle).click();

  await page.getByTestId(ids.filterSprint).selectOption('s25');
  await expect.poll(() => server.reads.at(-1)).toBe('?sprint=s25');
  await page.getByTestId(ids.filterSprint).selectOption('none');
  await expect.poll(() => server.reads.at(-1)).toBe('?sprint=none');
});

test('BoardFilter_WhenFiltered_ShouldKeepTheWipCountsAndSayCardsAreHidden', async ({
  mount,
  page,
}) => {
  await stubBoard(page, (search) => filtered(storiesBoard(), search));
  await mount(<BoardHarness />, stories);
  await expect(page.getByTestId(ids.filterNote)).toHaveCount(0);
  const counts = page.getByTestId(ids.columnCount);
  const before = COLUMNS.map((c) =>
    c.wipLimit === null ? `${c.count}` : `${c.count}/${c.wipLimit}`,
  );
  // Waits for the counts to show; reading them straight after the count of elements raced the first render.
  await expect(counts).toHaveText(before);
  await expect(page.getByTestId(ids.columnHidden)).toHaveCount(0);

  await page.getByTestId(ids.filterToggle).click();
  await page.getByTestId(ids.filterKeyword).fill('x');

  await expect(page.getByTestId(ids.filterNote)).toContainText(
    '3 cards hidden by the filter · WIP counts include hidden cards',
  );
  await expect(counts).toHaveText(before);
  // The column that lost the cards says so in its header, as the design does.
  await expect(page.getByTestId(ids.columnHidden)).toHaveText(['+3 hidden']);
  await expect(
    page.getByTestId(ids.columnHeader).first().getByTestId(ids.columnHidden),
  ).toBeVisible();
});

test('BoardFilter_WhenCleared_ShouldDropEveryParameterAndTheNote', async ({ mount, page }) => {
  const server = await stubBoard(page, (search) => filtered(storiesBoard(), search), {
    members: [TOM],
  });
  await mount(<BoardHarness />, stories);
  await page.getByTestId(ids.filterToggle).click();
  await page.getByTestId(ids.filterKeyword).fill('payout');
  await page.getByTestId(ids.filterPerson).filter({ hasText: 'Tom Okafor' }).click();
  await expect.poll(() => server.reads.at(-1)).toBe('?q=payout&assignee=u-tom');

  await page.getByTestId(ids.filterClear).click();

  await expect.poll(() => server.reads.at(-1)).toBe('');
  await expect(page.getByTestId(ids.filterKeyword)).toHaveValue('');
  await expect(page.getByTestId(ids.filterNote)).toHaveCount(0);
});

test('BoardFilter_WhenOnFeatures_ShouldOfferOwnerAndEpicNotSprint', async ({ mount, page }) => {
  const server = await stubBoard(page, (search) => filtered(featuresBoard(), search), {
    level: 'features',
    members: [TOM],
    epics: [{ id: 'e-90', title: 'Seller payouts' }],
  });
  await mount(<BoardHarness level="features" />, features);
  await page.getByTestId(ids.filterToggle).click();

  await expect(page.getByTestId(ids.filterSprint)).toHaveCount(0);
  await page.getByTestId(ids.filterPerson).filter({ hasText: 'No owner' }).click();
  await page.getByTestId(ids.filterPerson).filter({ hasText: 'Tom Okafor' }).click();
  await expect.poll(() => server.reads.at(-1)).toBe('?owner=none&owner=u-tom');
  await page.getByTestId(ids.filterEpic).selectOption('e-90');
  await expect.poll(() => server.reads.at(-1)).toBe('?owner=none&owner=u-tom&epic=e-90');
  await page.getByTestId(ids.filterEpic).selectOption('none');
  await expect.poll(() => server.reads.at(-1)).toBe('?owner=none&owner=u-tom&epic=none');
});

test('BoardFilter_WhenOnEpics_ShouldOfferNeitherSprintNorEpic', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard({ level: 'epics' }), { level: 'epics' });
  await mount(<BoardHarness level="epics" />, { hooksConfig: { route: '/p/SHIP/boards/epics' } });
  await page.getByTestId(ids.filterToggle).click();

  await expect(page.getByTestId(ids.filterKeyword)).toBeVisible();
  await expect(page.getByTestId(ids.filterSprint)).toHaveCount(0);
  await expect(page.getByTestId(ids.filterEpic)).toHaveCount(0);
});

test('FeaturesBoard_HealthFilter_ShouldHideOtherHealth', async ({ mount, page }) => {
  const server = await stubBoard(page, (search) => filteredByHealth(featuresWithHealth(), search), {
    level: 'features',
  });
  await mount(<BoardHarness level="features" />, features);
  await expect(page.getByTestId(ids.card)).toHaveCount(3);
  await page.getByTestId(ids.filterToggle).click();
  await expect(page.getByTestId(ids.filterHealth).locator('option')).toHaveText([
    'Any health',
    'On track',
    'At risk',
    'Blocked',
    'Not set',
  ]);

  await page.getByTestId(ids.filterHealth).selectOption('atRisk');
  await expect.poll(() => server.reads.at(-1)).toBe('?health=atRisk');
  await expect(page.getByTestId(ids.card)).toHaveCount(1);
  await expect(page.getByTestId(ids.cardKey)).toHaveText('SHIP-104');
  await expect(page.getByTestId(ids.filterToggle)).toHaveText('Filter (1)');

  await page.getByTestId(ids.filterHealth).selectOption('none');
  await expect.poll(() => server.reads.at(-1)).toBe('?health=none');
  await expect(page.getByTestId(ids.cardKey)).toHaveText('SHIP-90');
});

test('FeaturesBoard_HealthFilter_WhenTheBoardFailsToLoad_ShouldShowTheError', async ({
  mount,
  page,
}) => {
  await stubBoard(page, (search) => filteredByHealth(featuresWithHealth(), search), {
    level: 'features',
  });
  await mount(<BoardHarness level="features" />, features);
  await page.getByTestId(ids.filterToggle).click();
  await expect(page.getByTestId(ids.card)).toHaveCount(3);
  await page.route(/\/api\/boards\/[^/?]+\?health=/, (route) => route.fulfill({ status: 500 }));

  await page.getByTestId(ids.filterHealth).selectOption('blocked');

  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
});

// The usage reports the filter sends, as the URL's last segment and the body.
async function recordUsage(page: Page) {
  const sent: { report: string; body: unknown }[] = [];
  await page.route('**/api/projects/p1/usage/*', (route) => {
    sent.push({
      report: route.request().url().split('/').at(-1) ?? '',
      body: route.request().postDataJSON(),
    });
    return route.fulfill({ status: 204 });
  });
  return sent;
}

test('BoardFilter_WhenOpened_ShouldReportTheOpeningOnceNotTheClosing', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  const sent = await recordUsage(page);
  await mount(<BoardHarness />, stories);

  await page.getByTestId(ids.filterToggle).click();
  await expect(page.getByTestId(ids.filterBar)).toBeVisible();
  await expect.poll(() => sent).toEqual([{ report: 'board-filter-opened', body: {} }]);
  await page.getByTestId(ids.filterToggle).click();
  await expect(page.getByTestId(ids.filterBar)).toHaveCount(0);

  expect(sent).toHaveLength(1);
});

test('FeaturesBoard_HealthFilter_WhenChanged_ShouldReportTheChosenHealth', async ({
  mount,
  page,
}) => {
  await stubBoard(page, (search) => filteredByHealth(featuresWithHealth(), search), {
    level: 'features',
  });
  const sent = await recordUsage(page);
  await mount(<BoardHarness level="features" />, features);
  await page.getByTestId(ids.filterToggle).click();

  await page.getByTestId(ids.filterHealth).selectOption('atRisk');
  await page.getByTestId(ids.filterHealth).selectOption('none');
  await page.getByTestId(ids.filterHealth).selectOption('');

  await expect
    .poll(() => sent.filter((report) => report.report === 'board-health-filtered'))
    .toEqual([
      { report: 'board-health-filtered', body: { health: 'atRisk' } },
      { report: 'board-health-filtered', body: { health: 'none' } },
      { report: 'board-health-filtered', body: { health: 'any' } },
    ]);
});
