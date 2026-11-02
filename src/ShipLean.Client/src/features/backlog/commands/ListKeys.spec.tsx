import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { stubShell } from '../../shell/shellFixtures';
import { quickSearchTestIds } from '../../quickSearch/testIds';
import { ListKeysHarness } from '../ct/ListKeysHarness';
import { backlogTestIds as ids } from '../testIds';

const row = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.row}"][data-key="${key}"]`);
const log = (page: Page) => page.getByTestId('key-log');

async function mountList(mount: (node: React.JSX.Element) => Promise<unknown>, page: Page) {
  await stubShell(page);
  await page.route('**/api/search*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: '{"results":[]}' }),
  );
  await mount(<ListKeysHarness />);
}

test('J_and_K_move_focus', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();

  await page.keyboard.press('j');
  await expect(row(page, 'SHIP-2')).toBeFocused();
  // SHIP-3 is in a collapsed section.
  await page.keyboard.press('j');
  await expect(row(page, 'SHIP-4')).toBeFocused();
  await page.keyboard.press('j');
  await expect(row(page, 'SHIP-4')).toBeFocused();
  await page.keyboard.press('k');
  await expect(row(page, 'SHIP-2')).toBeFocused();
  await page.keyboard.press('Shift+k');
  await expect(row(page, 'SHIP-2')).toBeFocused();
  // One row is in the tab order.
  await expect(page.locator(`[data-testid="${ids.row}"][tabindex="0"]`)).toHaveCount(1);
  await expect(row(page, 'SHIP-2')).toHaveAttribute('tabindex', '0');
});

test('X_selects_the_focused_row_so_a_keyboard_reaches_every_row', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();

  await page.keyboard.press('x');
  await page.keyboard.press('j');
  await page.keyboard.press('j');
  await page.keyboard.press('x');

  await expect(row(page, 'SHIP-1')).toHaveAttribute('aria-selected', 'true');
  await expect(row(page, 'SHIP-4')).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByTestId('selected-count')).toHaveText('2');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId('selected-count')).toHaveText('0');
});

test('Keys_run_the_registered_commands', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();

  await page.keyboard.press('Enter');
  await page.keyboard.press('5');
  await page.keyboard.press('n');
  await page.keyboard.press('/');

  await expect(log(page)).toHaveText('open,estimate:5,addBelow,search');
});

test('Keys_do_nothing_in_a_field', async ({ mount, page }) => {
  await mountList(mount, page);
  await page.getByTestId('field').focus();

  await page.keyboard.type('jkx5n/?m2');
  await page.keyboard.press('Escape');

  await expect(page.getByTestId('field')).toHaveValue('jkx5n/?m2');
  await expect(log(page)).toHaveText('');
  await expect(page.getByTestId('selected-count')).toHaveText('0');
  await expect(page.getByTestId(ids.keysDialog)).toHaveCount(0);
});

test('Keys_do_nothing_in_a_dialog', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();
  await page.getByTestId('open-modal').click();
  await expect(page.getByRole('dialog')).toBeVisible();

  await page.keyboard.press('j');
  await page.keyboard.press('x');
  await page.keyboard.press('5');

  await expect(log(page)).toHaveText('');
  await expect(page.getByTestId('focused-id')).toHaveText('id-SHIP-1');
  await expect(page.getByTestId('selected-count')).toHaveText('0');
});

test('Keys_do_nothing_outside_the_list', async ({ mount, page }) => {
  await mountList(mount, page);
  await page.getByTestId('outside').focus();

  await page.keyboard.press('j');
  await page.keyboard.press('x');
  await page.keyboard.press('5');
  await page.keyboard.press('?');
  await page.keyboard.press('Escape');

  await expect(log(page)).toHaveText('');
  await expect(page.getByTestId('focused-id')).toHaveText('none');
  await expect(page.getByTestId(ids.keysDialog)).toHaveCount(0);
});

test('Ctrl_P_still_opens_quick_search', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();

  await page.keyboard.press('Control+p');

  await expect(page.getByTestId(quickSearchTestIds.dialog)).toBeVisible();
  await expect(log(page)).toHaveText('');
});

test('Alt_arrow_still_moves_the_row', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-2').focus();
  // The row's own handler (PlanningRow) takes Alt+Arrow; the list keys must neither run a command nor claim the event.
  const claimed = await row(page, 'SHIP-2').evaluate((el) => {
    const event = new KeyboardEvent('keydown', {
      key: 'ArrowUp',
      altKey: true,
      bubbles: true,
      cancelable: true,
    });
    el.dispatchEvent(event);
    const altJ = new KeyboardEvent('keydown', {
      key: 'j',
      altKey: true,
      bubbles: true,
      cancelable: true,
    });
    el.dispatchEvent(altJ);
    return [event.defaultPrevented, altJ.defaultPrevented];
  });

  expect(claimed).toEqual([false, false]);
  await expect(row(page, 'SHIP-2')).toBeFocused();
  await expect(log(page)).toHaveText('');
});

test('M_then_2_runs_moveMenu_with_2', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();

  await page.keyboard.press('m');
  await expect(log(page)).toHaveText('');
  await page.keyboard.press('2');
  await page.keyboard.press('m');
  await page.keyboard.press('t');
  await page.keyboard.press('m');
  await page.keyboard.press('b');

  await expect(log(page)).toHaveText('moveMenu:2,moveMenu:t,moveMenu:b');
});

test('M_then_Esc_runs_nothing', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();
  await page.keyboard.press('x');

  await page.keyboard.press('m');
  await page.keyboard.press('Escape');
  // The chord is over: a digit now starts an estimate, not a move.
  await page.keyboard.press('2');

  await expect(log(page)).toHaveText('estimate:2');
  // Esc only cancelled the chord; it did not clear the selection.
  await expect(page.getByTestId('selected-count')).toHaveText('1');
});

test('Question_mark_opens_the_list_and_Esc_closes_it', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();
  await page.keyboard.press('x');

  await page.keyboard.press('?');
  await expect(page.getByTestId(ids.keysDialog)).toBeVisible();
  await page.keyboard.press('Escape');

  await expect(page.getByTestId(ids.keysDialog)).toHaveCount(0);
  // Esc closed the list first; the selection stays.
  await expect(page.getByTestId('selected-count')).toHaveText('1');
  await page.getByTestId(ids.keysButton).click();
  await expect(page.getByTestId(ids.keysDialog)).toBeVisible();
});

test('Focus_survives_a_refetch', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-2').focus();

  await page.getByTestId('refetch').evaluate((button: HTMLElement) => button.click());

  await expect(row(page, 'SHIP-2')).toBeFocused();
  await expect(page.getByTestId('focused-id')).toHaveText('id-SHIP-2');
  await page.keyboard.press('j');
  await expect(row(page, 'SHIP-4')).toBeFocused();
});

test('Focus_moves_on_when_the_row_leaves', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-2').focus();

  // A programmatic click, so focus stays on the row until it is removed.
  await page.getByTestId('remove-2').evaluate((button: HTMLElement) => button.click());

  await expect(row(page, 'SHIP-2')).toHaveCount(0);
  await expect(row(page, 'SHIP-4')).toBeFocused();
  await expect(page.getByTestId('focused-id')).toHaveText('id-SHIP-4');
  await expect(row(page, 'SHIP-4')).toHaveAttribute('tabindex', '0');
});
