import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardViewDto } from '../../../api/views';
import { BoardHarness } from '../ct/BoardHarness';
import { card, cell, lane, storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

const cardKeyed = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });

const route = { hooksConfig: { route: '/p/SHIP/boards/stories' } };

// The default lane's Done cell shows one card and offers the other two, until its cursor is sent back as `older`.
function boardWithOlder(older: readonly string[], extraNew: string[] = []): BoardViewDto {
  const base = storiesBoard();
  const expanded = older.includes('done:lane-default');
  return {
    ...base,
    lanes: base.lanes.map((candidate) =>
      candidate.id !== 'lane-default'
        ? candidate
        : lane({
            ...candidate,
            cells: [
              cell(
                'col-new',
                extraNew.map((key) => card({ key })),
              ),
              cell('col-ready'),
              cell('col-ready', [], { doneHalf: true }),
              cell('col-prog'),
              cell(
                'col-done',
                expanded
                  ? [
                      card({ key: 'SHIP-140' }),
                      card({ key: 'SHIP-139' }),
                      card({ key: 'SHIP-138' }),
                    ]
                  : [card({ key: 'SHIP-140' })],
                expanded ? {} : { olderCount: 2, olderCursor: 'done:lane-default' },
              ),
            ],
          }),
    ),
  };
}

test('Board_WhenLoaded_ShouldFindTheLevelsBoardAndDrawItsColumns', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await expect(page.getByTestId(ids.columnHeader)).toHaveCount(4);
  await expect(page.getByTestId(ids.summary)).toHaveText('8 open · 30 pts');
  expect(server.reads[0]).toBe('');
});

test('Board_WhenShowOlderClickedAndRefetched_ShouldStayExpanded', async ({ mount, page }) => {
  const server = await stubBoard(page, (search) => boardWithOlder(search.getAll('older')));
  await mount(<BoardHarness />, route);

  const doneCards = page
    .getByTestId(ids.lane)
    .last()
    .getByTestId(ids.cell)
    .last()
    .getByTestId(ids.card);
  await expect(doneCards).toHaveCount(1);
  await page.getByTestId(ids.showOlder).click();
  await expect(doneCards).toHaveCount(3);
  await expect(page.getByTestId(ids.showOlder)).toHaveCount(0);
  expect(server.reads.at(-1)).toBe('?older=done%3Alane-default');

  const readsBefore = server.reads.length;
  await page.getByTestId('harness-refetch').click();
  await expect.poll(() => server.reads.length).toBeGreaterThan(readsBefore);
  expect(server.reads.at(-1)).toBe('?older=done%3Alane-default');
  await expect(doneCards).toHaveCount(3);
});

test('Board_WhenRefetchedWithANewCard_ShouldShowItWithoutCollapsingOlder', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, (search) => boardWithOlder(search.getAll('older')));
  await mount(<BoardHarness />, route);
  await page.getByTestId(ids.showOlder).click();
  await expect(cardKeyed(page, 'SHIP-138')).toBeVisible();

  server.setBoard(boardWithOlder(['done:lane-default'], ['SHIP-200']));
  await page.getByTestId('harness-refetch').click();
  await expect(cardKeyed(page, 'SHIP-200')).toBeVisible();
  await expect(cardKeyed(page, 'SHIP-138')).toBeVisible();
});

test('Board_WhenNewCardAdded_ShouldPostTheLevelsTypeAndRefetch', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);

  await page.getByTestId(ids.newCardOpen).click();
  await page.getByTestId(ids.newCardTitle).fill('  Offline mode  ');
  const reads = server.reads.length;
  await page.getByTestId(ids.newCardTitle).press('Enter');

  await expect(page.getByTestId(ids.newCardForm)).toHaveCount(0);
  expect(server.creates).toEqual([
    {
      projectId: 'p1',
      type: 'story',
      title: 'Offline mode',
      parentId: null,
      sprintId: null,
      changes: null,
    },
  ]);
  await expect.poll(() => server.reads.length).toBeGreaterThan(reads);
});

test('Board_WhenNewCardRefused_ShouldKeepTheFormAndSayWhy', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard(), {
    createStatus: 400,
    createBody: { code: 'validation', error: 'Title is too long.' },
  });
  await mount(<BoardHarness />, route);
  await page.getByTestId(ids.newCardOpen).click();
  await page.getByTestId(ids.newCardTitle).fill('x');
  await page.getByTestId(ids.newCardSubmit).click();
  await expect(page.getByTestId(ids.newCardError)).toContainText('Title is too long.');
  await expect(page.getByTestId(ids.newCardForm)).toBeVisible();
});

test('Board_WhenCompactChosen_ShouldRedrawCardsCompact', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);
  await expect(page.getByTestId(ids.card).first()).toHaveAttribute('data-compact', 'false');
  await page.getByTestId(ids.cardsOption).filter({ hasText: 'Compact' }).click();
  await expect(page.getByTestId(ids.card).first()).toHaveAttribute('data-compact', 'true');
});

test('Board_WhenBoardsListFails_ShouldSayItCouldNotLoad', async ({ mount, page }) => {
  await page.route('**/api/projects/p1/boards', (route) => route.fulfill({ status: 500 }));
  await mount(<BoardHarness />, route);
  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
});
