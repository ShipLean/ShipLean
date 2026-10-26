import { expect, test } from '@playwright/experimental-ct-react';
import type { ChildSummaryDto } from '../../../api/workItems';
import { LocationProbe } from '../ct/LocationProbe';
import { BoardHarness } from '../ct/BoardHarness';
import { TOM, rollupBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

const features = { hooksConfig: { route: '/p/SHIP/boards/features' } };

const child = (key: string, state: string, category: ChildSummaryDto['state']['category']) =>
  ({
    id: `id-${key}`,
    key,
    type: { id: 'story', name: 'User Story' },
    title: `Title of ${key}`,
    state: { id: `s-${state}`, name: state, category },
    assignee: TOM,
    points: 3,
  }) satisfies ChildSummaryDto;

const CHILDREN = [
  child('SHIP-141', 'Active', 'inProgress'),
  child('SHIP-142', 'New', 'proposed'),
  child('SHIP-143', 'Closed', 'completed'),
];

const rollup = (page: Parameters<Parameters<typeof test>[2]>[0]['page'], type: string) =>
  page.getByTestId(ids.cardRollup).and(page.locator(`[data-type="${type}"]`));

test('Board_WhenRollupClicked_ShouldListThatTypesChildrenWithTheirStates', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, rollupBoard(), {
    level: 'features',
    children: () => CHILDREN,
  });
  await mount(<BoardHarness level="features" />, features);
  await expect(page.getByTestId(ids.childList)).toHaveCount(0);

  await rollup(page, 'story').click();

  const rows = page.getByTestId(ids.childRow);
  await expect(rows).toHaveCount(3);
  await expect(rows.first()).toContainText('SHIP-141');
  await expect(rows.first().getByTestId(ids.childState)).toHaveText('Active');
  await expect(rows.last()).toHaveAttribute('data-closed', 'true');
  expect(server.childReads).toEqual(['id-SHIP-101 story']);
});

test('Board_WhenAnotherRollupClicked_ShouldAskForThatType', async ({ mount, page }) => {
  const server = await stubBoard(page, rollupBoard(), {
    level: 'features',
    children: () => [],
  });
  await mount(<BoardHarness level="features" />, features);

  await rollup(page, 'story').click();
  await expect(page.getByTestId(ids.childEmpty)).toBeVisible();
  await rollup(page, 'bug').click();

  await expect(page.getByTestId(ids.childList)).toHaveAttribute('data-type', 'bug');
  expect(server.childReads).toEqual(['id-SHIP-101 story', 'id-SHIP-101 bug']);
});

test('Board_WhenRollupClickedAgainOrEscape_ShouldCloseTheList', async ({ mount, page }) => {
  await stubBoard(page, rollupBoard(), { level: 'features' });
  await mount(<BoardHarness level="features" />, features);

  await rollup(page, 'story').click();
  await expect(page.getByTestId(ids.childList)).toBeVisible();
  await rollup(page, 'story').click();
  await expect(page.getByTestId(ids.childList)).toHaveCount(0);

  await rollup(page, 'story').click();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.childList)).toHaveCount(0);

  await rollup(page, 'story').click();
  await page.getByTestId(ids.childClose).click();
  await expect(page.getByTestId(ids.childList)).toHaveCount(0);
});

test('Board_WhenUserStoryAddedFromTheList_ShouldCreateItUnderTheCardAndRefetch', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, rollupBoard(), {
    level: 'features',
    children: () => CHILDREN,
  });
  await mount(<BoardHarness level="features" />, features);
  await rollup(page, 'story').click();
  await expect(page.getByTestId(ids.childAdd)).toHaveCount(2);

  await page.getByTestId(ids.childAdd).filter({ hasText: 'User story' }).click();
  await page.getByTestId(ids.childAddTitle).fill('  Export as CSV  ');
  const reads = server.reads.length;
  const childReads = server.childReads.length;
  await page.getByTestId(ids.childAddTitle).press('Enter');

  await expect(page.getByTestId(ids.childAddForm)).toHaveCount(0);
  expect(server.creates).toEqual([
    {
      projectId: 'p1',
      type: 'story',
      title: 'Export as CSV',
      parentId: 'id-SHIP-101',
      sprintId: null,
      changes: null,
    },
  ]);
  await expect.poll(() => server.reads.length).toBeGreaterThan(reads);
  await expect.poll(() => server.childReads.length).toBeGreaterThan(childReads);
});

test('Board_WhenBugAdded_ShouldCreateABug', async ({ mount, page }) => {
  const server = await stubBoard(page, rollupBoard(), { level: 'features' });
  await mount(<BoardHarness level="features" />, features);
  await rollup(page, 'bug').click();
  await page.getByTestId(ids.childAdd).filter({ hasText: 'Bug' }).click();
  await page.getByTestId(ids.childAddTitle).fill('Rounding off by a cent');
  await page.getByTestId(ids.childAddSubmit).click();

  await expect(page.getByTestId(ids.childAddForm)).toHaveCount(0);
  expect(server.creates.map((create) => create.type)).toEqual(['bug']);
});

test('Board_WhenChildRefused_ShouldKeepTheFormAndSayWhy', async ({ mount, page }) => {
  await stubBoard(page, rollupBoard(), {
    level: 'features',
    createStatus: 400,
    createBody: { code: 'validation', error: 'Title is too long.' },
  });
  await mount(<BoardHarness level="features" />, features);
  await rollup(page, 'story').click();
  await page.getByTestId(ids.childAdd).first().click();
  await page.getByTestId(ids.childAddTitle).fill('x');
  await page.getByTestId(ids.childAddSubmit).click();

  await expect(page.getByTestId(ids.childAddError)).toContainText('Title is too long.');
  await expect(page.getByTestId(ids.childAddForm)).toBeVisible();
});

test('Board_WhenOnEpics_ShouldOfferFeatureAsTheChildToAdd', async ({ mount, page }) => {
  const epic = rollupBoard({ level: 'epics' });
  await stubBoard(page, epic, { level: 'epics' });
  await mount(<BoardHarness level="epics" />, { hooksConfig: { route: '/p/SHIP/boards/epics' } });
  await rollup(page, 'story').click();

  await expect(page.getByTestId(ids.childAdd)).toHaveCount(1);
  await expect(page.getByTestId(ids.childAdd)).toHaveAttribute('data-type', 'feature');
});

test('Board_WhenACardMovesWithTheChildListOpen_ShouldStillMove', async ({ mount, page }) => {
  const server = await stubBoard(page, rollupBoard(), {
    level: 'features',
    children: () => CHILDREN,
  });
  await mount(<BoardHarness level="features" />, features);
  await rollup(page, 'story').click();
  await expect(page.getByTestId(ids.childRow)).toHaveCount(3);

  const card = page.getByTestId(ids.card).filter({ hasText: 'SHIP-101' });
  await card.focus();
  await page.keyboard.press('Alt+ArrowRight');

  await expect.poll(() => server.moves.length).toBe(1);
  await expect(page.getByTestId(ids.childRow)).toHaveCount(3);
  await expect(page.getByTestId(ids.childList)).toBeVisible();
});

test('Board_WhenARollupIsFocusedAndEnterPressed_ShouldOpenItsList', async ({ mount, page }) => {
  await stubBoard(page, rollupBoard(), { level: 'features' });
  await mount(<BoardHarness level="features" />, features);

  await rollup(page, 'bug').focus();
  await page.keyboard.press('Enter');

  await expect(page.getByTestId(ids.childList)).toHaveAttribute('data-type', 'bug');
});

test('Board_WhenTheListOpensAndIsEscaped_ShouldMoveFocusInAndBackToTheRollup', async ({
  mount,
  page,
}) => {
  await stubBoard(page, rollupBoard(), { level: 'features', children: () => CHILDREN });
  await mount(<BoardHarness level="features" />, features);

  await rollup(page, 'story').click();
  await expect(page.getByTestId(ids.childList)).toBeFocused();
  await expect(page.getByTestId(ids.childList)).toContainText('User stories');
  await page.keyboard.press('Escape');

  await expect(page.getByTestId(ids.childList)).toHaveCount(0);
  await expect(rollup(page, 'story')).toBeFocused();
});

test('Board_WhenTheBoardScrollsSideways_ShouldCloseTheList', async ({ mount, page }) => {
  await stubBoard(page, rollupBoard(), { level: 'features', children: () => CHILDREN });
  await mount(
    <div data-testid="scroller" style={{ width: 500, overflow: 'auto' }}>
      <BoardHarness level="features" />
    </div>,
    features,
  );
  await rollup(page, 'story').click();
  await expect(page.getByTestId(ids.childList)).toBeVisible();

  // The board's own scroller: the nearest ancestor of a card that overflows sideways.
  await page
    .getByTestId(ids.card)
    .first()
    .evaluate((card) => {
      for (let node = card.parentElement; node; node = node.parentElement) {
        if (node.scrollWidth > node.clientWidth && getComputedStyle(node).overflowX !== 'visible') {
          node.scrollLeft += 40;
          return;
        }
      }
    });

  await expect(page.getByTestId(ids.childList)).toHaveCount(0);
});

test('Board_WhenAFeatureHasNoChildren_ShouldOfferNoStoriesYetToAddTheFirst', async ({
  mount,
  page,
}) => {
  const board = rollupBoard();
  board.lanes[0]!.cells[0]!.cards[0]!.rollups = [];
  await stubBoard(page, board, { level: 'features', children: () => [] });
  await mount(<BoardHarness level="features" />, features);
  await expect(rollup(page, 'story')).toHaveText('No stories yet');

  await rollup(page, 'story').click();

  await expect(page.getByTestId(ids.childEmpty)).toBeVisible();
  await expect(page.getByTestId(ids.childAdd)).toHaveCount(2);
});

test('Board_WhenAnEpicHasNoFeatures_ShouldOfferNoFeaturesYet', async ({ mount, page }) => {
  const board = rollupBoard({ level: 'epics' });
  board.lanes[0]!.cells[0]!.cards[0]!.rollups = [];
  await stubBoard(page, board, { level: 'epics', children: () => [] });
  await mount(<BoardHarness level="epics" />, { hooksConfig: { route: '/p/SHIP/boards/epics' } });
  await expect(rollup(page, 'feature')).toHaveText('No Features yet');

  await rollup(page, 'feature').click();

  await expect(page.getByTestId(ids.childAdd)).toHaveAttribute('data-type', 'feature');
});

test('Board_WhenChildClicked_ShouldOpenChildInModal', async ({ mount, page }) => {
  await stubBoard(page, rollupBoard(), { level: 'features', children: () => CHILDREN });
  await mount(
    <>
      <BoardHarness level="features" />
      <LocationProbe />
    </>,
    features,
  );
  await rollup(page, 'story').click();

  await page.getByTestId(ids.childRow).first().getByTestId('item-link').click();

  await expect(page.getByTestId(ids.locationProbe)).toHaveText(
    '/p/SHIP/boards/features?item=SHIP-141',
  );
});
