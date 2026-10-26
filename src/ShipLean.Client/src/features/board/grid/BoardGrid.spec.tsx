import { expect, test } from '@playwright/experimental-ct-react';
import { boardTestIds as ids } from '../testIds';
import { GridHarness } from '../ct/GridHarness';
import { cell, COLUMNS, lane, storiesBoard } from '../ct/fixtures';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

// A lane by its header name, a card by its key text: test ids only.
const laneNamed = (page: Page, name: string) =>
  page
    .getByTestId(ids.lane)
    .filter({ has: page.getByTestId(ids.laneName).filter({ hasText: name }) });
const cardKeyed = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });

test('Grid_WhenColumnSplit_ShouldSpanTwoTracksWithDoingAndDoneHalves', async ({ mount, page }) => {
  await mount(<GridHarness view={storiesBoard()} />);

  const headers = page.getByTestId(ids.columnHeader);
  await expect(headers).toHaveCount(4);
  const ready = headers.nth(1);
  await expect(ready.getByTestId(ids.subDoing)).toHaveText('Doing 2');
  await expect(ready.getByTestId(ids.subDone)).toHaveText('Done 1');
  await expect(headers.nth(0).getByTestId(ids.subDoing)).toHaveCount(0);

  const cells = laneNamed(page, 'Default lane').getByTestId(ids.cell);
  await expect(cells).toHaveCount(5);
  const readyDoing = cells.nth(1);
  const readyDone = cells.nth(2);
  await expect(readyDoing.getByTestId(ids.cardKey)).toHaveText('SHIP-161');
  await expect(readyDone.getByTestId(ids.cardKey)).toHaveText('SHIP-162');
  const [doing, done] = await Promise.all([readyDoing.boundingBox(), readyDone.boundingBox()]);
  expect(done!.x).toBeGreaterThan(doing!.x);
});

test('Grid_WhenOverWip_ShouldMarkTheHeaderDangerWithoutHidingCards', async ({ mount, page }) => {
  await mount(<GridHarness view={storiesBoard()} />);

  const progress = page.getByTestId(ids.columnHeader).nth(2);
  await expect(progress).toHaveAttribute('data-over', 'true');
  await expect(progress.getByTestId(ids.columnCount)).toHaveText('3/2');
  const ready = page.getByTestId(ids.columnHeader).nth(1);
  await expect(ready).toHaveAttribute('data-over', 'false');
  await expect(ready.getByTestId(ids.columnCount)).toHaveText('3/8');
  await expect(page.getByTestId(ids.columnHeader).nth(0).getByTestId(ids.columnCount)).toHaveText(
    '2',
  );
  await expect(cardKeyed(page, 'SHIP-150')).toBeVisible();
});

test('Grid_WhenColumnMapsStates_ShouldListThemOnceUnderTheName', async ({ mount, page }) => {
  await mount(<GridHarness view={storiesBoard()} />);
  await expect(page.getByTestId(ids.columnMap).nth(1)).toHaveText('New · Triaged');
});

test('Grid_WhenLaneCollapsed_ShouldHideItsCellsKeepHeaderAndExpandAgain', async ({
  mount,
  page,
}) => {
  await mount(<GridHarness view={storiesBoard()} />);

  const expedite = laneNamed(page, 'Expedite');
  await expect(expedite.getByTestId(ids.laneName)).toHaveText('Expedite');
  await expect(expedite.getByTestId(ids.laneCriteria)).toHaveText('Priority = 1');
  await expect(expedite.getByTestId(ids.laneCount)).toHaveText('1 card');
  await expect(expedite.getByTestId(ids.lanePoints)).toHaveText('5 pts open');
  await expect(expedite.getByTestId(ids.cell)).toHaveCount(5);

  await expedite.getByTestId(ids.laneToggle).click();
  await expect(expedite).toHaveAttribute('data-collapsed', 'true');
  await expect(expedite.getByTestId(ids.cell)).toHaveCount(0);
  await expect(expedite.getByTestId(ids.laneName)).toBeVisible();
  const other = laneNamed(page, 'Default lane');
  await expect(other.getByTestId(ids.cell)).toHaveCount(5);

  await expedite.getByTestId(ids.laneToggle).click();
  await expect(expedite.getByTestId(ids.cell)).toHaveCount(5);
});

test('Grid_WhenOneDefaultLane_ShouldShowNoLaneHeader', async ({ mount, page }) => {
  const view = storiesBoard({
    lanes: [
      lane({
        id: 'lane-default',
        kind: 'default',
        cells: COLUMNS.map((column) => cell(column.id)),
      }),
    ],
  });
  await mount(<GridHarness view={view} />);
  await expect(page.getByTestId(ids.laneHeader)).toHaveCount(0);
  await expect(page.getByTestId(ids.cell)).toHaveCount(5);
});

test('Grid_WhenSettingsChangedElsewhere_ShouldFlashEveryHeader', async ({ mount, page }) => {
  await mount(<GridHarness view={storiesBoard()} flashes={2} />);
  const headers = page.getByTestId(ids.columnHeader);
  for (let index = 0; index < 4; index++) {
    await expect(headers.nth(index)).toHaveAttribute('data-flashes', '2');
  }
});

test('Grid_WhenLanesHaveADefaultLane_ShouldOfferNewOnlyInItsFirstCell', async ({ mount, page }) => {
  await mount(<GridHarness view={storiesBoard()} />);
  await expect(page.getByTestId(ids.newCardOpen)).toHaveCount(1);
  const cells = laneNamed(page, 'Default lane').getByTestId(ids.cell);
  await expect(cells.nth(0).getByTestId(ids.newCardOpen)).toBeVisible();
  await expect(laneNamed(page, 'Expedite').getByTestId(ids.newCardOpen)).toHaveCount(0);
});

test('Grid_WhenNoDefaultLane_ShouldOfferNewInTheFirstLane', async ({ mount, page }) => {
  const base = storiesBoard();
  const lanes = base.lanes.map((candidate) => ({ ...candidate, kind: 'criteria' as const }));
  await mount(<GridHarness view={storiesBoard({ lanes })} />);
  await expect(page.getByTestId(ids.newCardOpen)).toHaveCount(1);
  await expect(laneNamed(page, 'Expedite').getByTestId(ids.newCardOpen)).toBeVisible();
});

test('Grid_WhenEpicBoardHasANoEpicLane_ShouldOfferNewThere', async ({ mount, page }) => {
  const base = storiesBoard();
  const lanes = base.lanes.map((candidate, index) => ({
    ...candidate,
    kind: index === 1 ? ('noEpic' as const) : ('epic' as const),
  }));
  await mount(<GridHarness view={storiesBoard({ lanes })} />);
  await expect(laneNamed(page, 'Default lane').getByTestId(ids.newCardOpen)).toBeVisible();
  await expect(page.getByTestId(ids.newCardOpen)).toHaveCount(1);
});

test('Grid_WhenShowOlderClicked_ShouldAskToExpandThatCellsCursor', async ({ mount, page }) => {
  await mount(<GridHarness view={storiesBoard()} />);
  const older = page.getByTestId(ids.showOlder);
  await expect(older).toHaveText('Show older (2)');
  await older.click();
  await expect(page.getByTestId('expanded')).toHaveText('done:lane-default');
});
