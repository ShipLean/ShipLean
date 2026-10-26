import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardViewDto } from '../../../api/views';
import { BoardHarness } from '../ct/BoardHarness';
import { COLUMNS, TOM, column, featuresBoard, storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
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
