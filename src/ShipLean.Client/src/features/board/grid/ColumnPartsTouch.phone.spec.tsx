import { expect, test } from '@playwright/experimental-ct-react';
import { ColumnPartsHarness } from '../ct/ColumnPartsHarness';
import { GridHarness } from '../ct/GridHarness';
import { cell, featuresBoard } from '../ct/fixtures';
import { boardTestIds as ids } from '../testIds';

// The column header, Show older and + New at touch sizes (SHIP-719, L719f), each on its own at 390 wide. Runs in the CT
// phone project. Element shots cover this task's parts only. Regenerate with
// scripts/update-page-shots.sh src/features/board/grid/ColumnPartsTouch.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const TOUCH = 32;

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
const box = async (page: Page, testId: string) => {
  const found = await page.getByTestId(testId).boundingBox();
  expect(found, `${testId} is on screen`).not.toBeNull();
  return found!;
};

test('ColumnHeader_WhenTouch_ShouldShowNameWipStateAndInfoInOneRow', async ({ mount, page }) => {
  await mount(<ColumnPartsHarness part="header" />);

  await expect(page.getByTestId(ids.columnName)).toHaveText('In progress');
  await expect(page.getByTestId(ids.columnCount)).toHaveText('3/2');
  await expect(page.getByTestId(ids.columnMap)).toHaveText('Active · Fixing');
  const name = await box(page, ids.columnName);
  const count = await box(page, ids.columnCount);
  const info = await box(page, ids.dodOpen);
  const map = await box(page, ids.columnMap);
  // Name, WIP and ⓘ share a row, left to right; the states sit under it.
  expect(Math.abs(name.y + name.height / 2 - (count.y + count.height / 2))).toBeLessThan(4);
  expect(Math.abs(name.y + name.height / 2 - (info.y + info.height / 2))).toBeLessThan(4);
  expect(name.x).toBeLessThan(count.x);
  expect(count.x).toBeLessThan(info.x);
  expect(map.y).toBeGreaterThan(name.y + name.height - 1);
  expect(info.width).toBeGreaterThanOrEqual(TOUCH);
  expect(info.height).toBeGreaterThanOrEqual(TOUCH);
});

test('ColumnHeader_WhenOverWip_ShouldShowDanger', async ({ mount, page }) => {
  await mount(<ColumnPartsHarness part="header" over />);

  await expect(page.getByTestId(ids.columnHeader)).toHaveAttribute('data-over', 'true');
  await expect(page.getByTestId(ids.columnCount)).toHaveClass(/text-alert/);
  await expect(page.getByTestId(ids.columnHeader)).toHaveCSS(
    'border-top-color',
    await page.evaluate(() => {
      const probe = document.createElement('i');
      probe.style.color = 'var(--color-danger)';
      document.body.append(probe);
      const resolved = getComputedStyle(probe).color;
      probe.remove();
      return resolved;
    }),
  );
});

test('ColumnHeader_WhenFeaturesLevel_ShouldShowTheFeatureStates', async ({ mount, page }) => {
  await mount(<ColumnPartsHarness part="header" level="features" />);
  await expect(page.getByTestId(ids.columnMap)).toHaveText('Building');
  await expect(page.getByTestId(ids.columnCount)).toHaveText('4');
});

test('ColumnHeader_WhenTouch_ShouldNotBeSticky', async ({ mount, page }) => {
  await mount(<ColumnPartsHarness part="header" />);

  const positions = await page.getByTestId(ids.columnHeader).evaluate((element) => {
    const found: string[] = [];
    for (let at: Element | null = element; at; at = at.parentElement) {
      found.push(getComputedStyle(at).position);
    }
    return found;
  });
  expect(positions).not.toContain('sticky');
  expect(positions).not.toContain('fixed');
});

test('NewCard_WhenOpenedAtTouch_ShouldHave16pxFieldAnd32pxButtons', async ({ mount, page }) => {
  await mount(<ColumnPartsHarness part="newCard" />);

  const opener = await box(page, ids.newCardOpen);
  expect(opener.height).toBeGreaterThanOrEqual(TOUCH);
  expect(opener.width).toBeGreaterThan(300);

  await page.getByTestId(ids.newCardOpen).tap();
  await expect(page.getByTestId(ids.newCardTitle)).toHaveCSS('font-size', '16px');
  for (const id of [ids.newCardSubmit, ids.newCardCancel, ids.newCardTitle]) {
    expect((await box(page, id)).height, id).toBeGreaterThanOrEqual(TOUCH);
  }
});

test('Cell_WhenEmptyOnHeaderlessBoardAtTouch_ShouldShowNoCards', async ({ mount, page }) => {
  await mount(<GridHarness view={featuresBoard()} level="features" />);

  // The Ready column is split, so its Doing and Done cells are both empty.
  const empty = page.getByTestId(ids.cellEmpty);
  await expect(empty).toHaveCount(2);
  await expect(empty.first()).toHaveText('No cards');
  await expect(empty.first()).toHaveCSS('display', 'block');
});

test('Cell_WhenItOffersNewOrHasOlderCards_ShouldNotShowNoCards', async ({ mount, page }) => {
  const base = featuresBoard();
  const view = {
    ...base,
    lanes: base.lanes.map((lane) => ({
      ...lane,
      // New holds only "+ New", and Done holds only older cards behind Show older.
      cells: lane.cells.map((one) =>
        one.columnId === 'col-new'
          ? cell('col-new')
          : one.columnId === 'col-done'
            ? cell('col-done', [], { olderCount: 2, olderCursor: 'done:lane' })
            : one,
      ),
    })),
  };
  await mount(<GridHarness view={view} level="features" />);

  await expect(page.getByTestId(ids.newCardOpen)).toHaveCount(1);
  await expect(page.getByTestId(ids.showOlder)).toHaveCount(1);
  // Only Ready's two empty cells say it.
  await expect(page.getByTestId(ids.cellEmpty)).toHaveCount(2);
  await expect(
    page.getByTestId(ids.cell).filter({ has: page.getByTestId(ids.newCardOpen) }),
  ).not.toContainText('No cards');
});

test('ColumnHeader_WhenInfoOpenedAtTouch_ShouldKeepThePopoverButtonItsNaturalSize', async ({
  mount,
  page,
}) => {
  await mount(<ColumnPartsHarness part="header" />);
  await page.getByTestId(ids.dodOpen).tap();

  const edit = await box(page, ids.dodSettings);
  expect(edit.height).toBeLessThan(TOUCH);
});

test('ShowOlder_WhenTouch_ShouldBeAFullWidth32pxRow', async ({ mount, page }) => {
  await mount(<ColumnPartsHarness part="showOlder" />);

  const row = await box(page, ids.showOlder);
  const wrap = await box(page, 'column-part');
  expect(row.height).toBeGreaterThanOrEqual(TOUCH);
  expect(row.width).toBeGreaterThan(wrap.width - 20);
});

for (const theme of THEMES) {
  for (const [name, level] of [
    ['board-column-header', 'stories'],
    ['features-board-column-header', 'features'],
    ['epics-board-column-header', 'epics'],
  ] as const) {
    test(`ColumnPartsPhone_WhenHeaderShown_ShouldMatchItsScreenshot_${name}_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      const screen = await mount(
        <ColumnPartsHarness part="header" level={level} over={level === 'stories'} />,
      );
      await expect(screen).toHaveScreenshot(`${name}.${theme}.png`, { animations: 'disabled' });
    });
  }

  test(`ColumnPartsPhone_WhenNewCardOpened_ShouldMatchItsScreenshot_board-new-card_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const screen = await mount(<ColumnPartsHarness part="newCard" />);
    await page.getByTestId(ids.newCardOpen).tap();
    await expect(page.getByTestId(ids.newCardTitle)).toBeVisible();
    await expect(screen).toHaveScreenshot(`board-new-card.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
