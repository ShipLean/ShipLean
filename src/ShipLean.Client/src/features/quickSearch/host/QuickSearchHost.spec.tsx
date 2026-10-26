import { expect, test } from '@playwright/experimental-ct-react';
import searchView from '../../../../../ShipLean.TestSupport/DtoFixtures/search/search-view.json' with { type: 'json' };
import { shellHarnessIds as harness } from '../../shell/ct/harnessIds';
import { ShellHarness } from '../../shell/ct/ShellHarness';
import { stubShell } from '../../shell/shellFixtures';
import { shellTestIds as shell } from '../../shell/testIds';
import { quickSearchTestIds as ids } from '../testIds';
import { OpenItemHarness, openItemHarnessIds as openItem } from './ct/OpenItemHarness';

const at = (route: string) => ({ hooksConfig: { route } });
const board = '/p/SHIP/boards/stories';

async function stubSearch(page: Parameters<typeof stubShell>[0]) {
  await stubShell(page);
  await page.route('**/api/search*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(searchView),
    }),
  );
}

test('QuickSearch_WhenControlPPressed_ShouldOpenWithTheInputFocused', async ({ mount, page }) => {
  await stubSearch(page);
  await mount(<ShellHarness />, at(board));
  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
  await page.keyboard.press('Control+p');
  await expect(page.getByTestId(ids.dialog)).toBeVisible();
  await expect(page.getByTestId(ids.input)).toBeFocused();
});

test('QuickSearch_WhenControlPPressedWhileOpen_ShouldSelectTheInput', async ({ mount, page }) => {
  await stubSearch(page);
  await mount(<ShellHarness />, at(board));
  await page.keyboard.press('Control+p');
  await page.getByTestId(ids.input).fill('abc');
  await page.getByTestId(ids.input).evaluate((input: HTMLInputElement) => {
    input.setSelectionRange(3, 3);
    input.blur();
  });
  await page.keyboard.press('Control+p');
  await expect(page.getByTestId(ids.dialog)).toHaveCount(1);
  await expect(page.getByTestId(ids.input)).toBeFocused();
  const selected = await page
    .getByTestId(ids.input)
    .evaluate((input: HTMLInputElement) => [input.selectionStart, input.selectionEnd]);
  expect(selected).toEqual([0, 3]);
});

test('QuickSearch_WhenControlPPressedInAModalDialog_ShouldNotOpen', async ({ mount, page }) => {
  await stubSearch(page);
  await mount(<ShellHarness />, at(board));
  await page.getByTestId(harness.openDialog).click();
  await expect(page.getByTestId(harness.dialogBody)).toBeVisible();
  await page.keyboard.press('Control+p');
  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
});

test('QuickSearch_WhenControlPPressedWithThePickerOpen_ShouldCloseThePicker', async ({
  mount,
  page,
}) => {
  await stubSearch(page);
  await mount(<ShellHarness />, at(board));
  await page.getByTestId(shell.projectButton).click();
  await expect(page.getByTestId(shell.picker)).toBeVisible();
  await page.keyboard.press('Control+p');
  await expect(page.getByTestId(ids.dialog)).toBeVisible();
  await expect(page.getByTestId(shell.picker)).toHaveCount(0);
});

test('QuickSearch_WhenEscapePressed_ShouldCloseAndRefocusTheOpener', async ({ mount, page }) => {
  await stubSearch(page);
  await mount(<ShellHarness />, at(board));
  await page.getByTestId(shell.searchButton).click();
  await expect(page.getByTestId(ids.dialog)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
  await expect(page.getByTestId(shell.searchButton)).toBeFocused();
});

test('QuickSearch_WhenReopened_ShouldShowTheLastQuerySelected', async ({ mount, page }) => {
  await stubSearch(page);
  await mount(<ShellHarness />, at(board));
  await page.keyboard.press('Control+p');
  await page.getByTestId(ids.input).fill('export');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
  await page.keyboard.press('Control+p');
  await expect(page.getByTestId(ids.input)).toHaveValue('export');
  const selected = await page
    .getByTestId(ids.input)
    .evaluate((input: HTMLInputElement) => [input.selectionStart, input.selectionEnd]);
  expect(selected).toEqual([0, 6]);
});

test('QuickSearch_WhenSearchButtonClicked_ShouldOpen', async ({ mount, page }) => {
  await stubSearch(page);
  await mount(<ShellHarness />, at(board));
  const button = page.getByTestId(shell.searchButton);
  await expect(button).toHaveAttribute('aria-label', 'Quick search');
  await button.click();
  await expect(page.getByTestId(ids.dialog)).toBeVisible();
});

test('QuickSearch_WhenNarrow_ShouldOfferTheIconButton', async ({ mount, page }) => {
  await stubSearch(page);
  await page.setViewportSize({ width: 800, height: 700 });
  await mount(<ShellHarness />, at(board));
  await expect(page.getByTestId(shell.searchButton)).toBeHidden();
  await page.getByTestId(shell.searchIconButton).click();
  await expect(page.getByTestId(ids.dialog)).toBeVisible();
});

test('QuickSearch_WhenWide_ShouldHideTheIconButton', async ({ mount, page }) => {
  await stubSearch(page);
  await mount(<ShellHarness />, at(board));
  await expect(page.getByTestId(shell.searchIconButton)).toBeHidden();
});

test('OpenItem_WhenOnABoardOfTheSameProject_ShouldOpenTheModal', async ({ mount, page }) => {
  await mount(<OpenItemHarness />, at(board));
  await page.getByTestId(openItem.open).click();
  await expect(page.getByTestId(openItem.location)).toHaveText(`${board}?item=SHIP-9`);
});

test('OpenItem_WhenOnABacklog_ShouldOpenTheFullPage', async ({ mount, page }) => {
  await mount(<OpenItemHarness />, at('/p/SHIP/backlogs/stories'));
  await page.getByTestId(openItem.open).click();
  await expect(page.getByTestId(openItem.location)).toHaveText('/p/SHIP/items/SHIP-9');
});
