import { expect, test } from '@playwright/experimental-ct-react';
import { boardTestIds as ids } from '../testIds';
import { GridHarness } from '../ct/GridHarness';
import { LocationProbe } from '../ct/LocationProbe';
import { COLUMNS, storiesBoard } from '../ct/fixtures';

const withDod = () =>
  storiesBoard({
    columns: COLUMNS.map((column) =>
      column.id === 'col-ready'
        ? { ...column, definitionOfDoneHtml: '<ul><li>PR approved</li><li>CI green</li></ul>' }
        : column,
    ),
  });

test('Dod_WhenOpened_ShouldShowStatePerTypeAndTheRenderedDefinition', async ({ mount, page }) => {
  await mount(<GridHarness view={withDod()} />);
  await expect(page.getByTestId(ids.dod)).toHaveCount(0);

  await page.getByTestId(ids.columnHeader).nth(1).getByTestId(ids.dodOpen).click();

  const popover = page.getByTestId(ids.dod);
  await expect(popover).toBeVisible();
  await expect(popover.getByTestId(ids.dodType)).toHaveText([/User Story$/, /Bug$/]);
  await expect(popover.getByTestId(ids.dodState)).toHaveText(['New', 'Triaged']);
  await expect(popover.getByTestId(ids.dodBody).locator('li')).toHaveText([
    'PR approved',
    'CI green',
  ]);
  await expect(popover.getByTestId(ids.dodWip)).toContainText(
    'WIP limit 8, counting Doing and Done',
  );
  await expect(popover.getByTestId(ids.dodSplit)).toBeVisible();
});

test('Dod_WhenColumnHasNoWipOrSplit_ShouldOmitThoseNotes', async ({ mount, page }) => {
  await mount(<GridHarness view={withDod()} />);
  await page.getByTestId(ids.columnHeader).nth(0).getByTestId(ids.dodOpen).click();
  const popover = page.getByTestId(ids.dod);
  await expect(popover.getByTestId(ids.dodWip)).toHaveCount(0);
  await expect(popover.getByTestId(ids.dodSplit)).toHaveCount(0);
  await expect(popover.getByTestId(ids.dodEmpty)).toBeVisible();
  await expect(popover.getByTestId(ids.dodBody)).toHaveCount(0);
});

test('Dod_WhenToggledEscapedOrClickedAway_ShouldClose', async ({ mount, page }) => {
  await mount(<GridHarness view={withDod()} />);
  const open = page.getByTestId(ids.columnHeader).nth(1).getByTestId(ids.dodOpen);
  const popover = page.getByTestId(ids.dod);

  await open.click();
  await expect(popover).toBeVisible();
  await open.click();
  await expect(popover).toHaveCount(0);

  await open.click();
  await page.keyboard.press('Escape');
  await expect(popover).toHaveCount(0);
  await expect(open).toBeFocused();

  await open.click();
  await page.getByTestId(ids.columnName).first().click();
  await expect(popover).toHaveCount(0);
});

test('Dod_WhenOpenedOnAnotherColumn_ShouldShowThatColumnsPopover', async ({ mount, page }) => {
  await mount(<GridHarness view={withDod()} />);
  await page.getByTestId(ids.columnHeader).nth(1).getByTestId(ids.dodOpen).click();
  await page.getByTestId(ids.columnHeader).nth(2).getByTestId(ids.dodOpen).click();
  await expect(page.getByTestId(ids.dod)).toHaveCount(1);
  await expect(page.getByTestId(ids.dod)).toContainText('In progress');
  await expect(page.getByTestId(ids.dodState)).toHaveText(['Active']);
});

test('Dod_WhenFocusMovedOnAndEscapePressed_ShouldCloseAndFocusTheOpener', async ({
  mount,
  page,
}) => {
  await mount(<GridHarness view={withDod()} />);
  const open = page.getByTestId(ids.columnHeader).nth(1).getByTestId(ids.dodOpen);
  await open.click();
  await page.keyboard.press('Tab');
  await expect(page.getByTestId(ids.dod)).toBeVisible();
  await expect(open).not.toBeFocused();

  await page.keyboard.press('Escape');

  await expect(page.getByTestId(ids.dod)).toHaveCount(0);
  await expect(open).toBeFocused();
});

test('Dod_WhenASettingsFlashArrives_ShouldKeepKeyboardFocusOnTheOpener', async ({
  mount,
  page,
}) => {
  const component = await mount(<GridHarness view={withDod()} flashes={0} />);
  const open = page.getByTestId(ids.columnHeader).nth(1).getByTestId(ids.dodOpen);
  await open.focus();
  await expect(open).toBeFocused();

  await component.update(<GridHarness view={withDod()} flashes={1} />);

  await expect(page.getByTestId(ids.columnHeader).nth(1)).toHaveAttribute('data-flashes', '1');
  await expect(open).toBeFocused();
});

test('Dod_WhenTheBoardScrollsSideways_ShouldClose', async ({ mount, page }) => {
  await mount(
    <div data-testid="scroller" style={{ width: 500, overflow: 'auto' }}>
      <GridHarness view={withDod()} />
    </div>,
  );
  await page.getByTestId(ids.columnHeader).nth(1).getByTestId(ids.dodOpen).click();
  await expect(page.getByTestId(ids.dod)).toBeVisible();

  await page.getByTestId('scroller').evaluate((element) => {
    element.scrollLeft += 40;
  });

  await expect(page.getByTestId(ids.dod)).toHaveCount(0);
});

test('Dod_WhenThePageScrollsVertically_ShouldStayOpen', async ({ mount, page }) => {
  await mount(<GridHarness view={withDod()} />);
  await page.getByTestId(ids.columnHeader).nth(1).getByTestId(ids.dodOpen).click();

  await page.evaluate(() => document.dispatchEvent(new Event('scroll')));

  await expect(page.getByTestId(ids.dod)).toBeVisible();
});

test('Dod_WhenEditInBoardSettingsClicked_ShouldOpenTheColumnsPaneAndClose', async ({
  mount,
  page,
}) => {
  await mount(
    <>
      <GridHarness view={withDod()} />
      <LocationProbe />
    </>,
  );
  await page.getByTestId(ids.columnHeader).nth(1).getByTestId(ids.dodOpen).click();

  await page.getByTestId(ids.dodSettings).click();

  await expect(page.getByTestId(ids.locationProbe)).toContainText('settings=columns');
  await expect(page.getByTestId(ids.dod)).toHaveCount(0);
});
