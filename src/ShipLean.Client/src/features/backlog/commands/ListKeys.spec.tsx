import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { stubShell } from '../../shell/shellFixtures';
import { quickSearchTestIds } from '../../quickSearch/testIds';
import { ListKeysHarness } from '../ct/ListKeysHarness';
import { PROJECT, planningManyView, planningView } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { PlanningMode } from '../planning/PlanningMode';
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

// Proves the keys are live: J on the first row moves focus to the second. A test that then shows a key doing nothing
// somewhere else means something only because this holds.
async function proveKeysAreLive(page: Page) {
  await row(page, 'SHIP-1').focus();
  await page.keyboard.press('j');
  await expect(row(page, 'SHIP-2')).toBeFocused();
  await expect(page.getByTestId('focused-id')).toHaveText('id-SHIP-2');
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
  await proveKeysAreLive(page);
  await page.getByTestId('field').focus();

  await page.keyboard.type('jkx5n/?m2');
  await page.keyboard.press('Escape');

  await expect(page.getByTestId('field')).toHaveValue('jkx5n/?m2');
  await expect(page.getByTestId('focused-id')).toHaveText('id-SHIP-2');
  await expect(log(page)).toHaveText('');
  await expect(page.getByTestId('selected-count')).toHaveText('0');
  await expect(page.getByTestId(ids.keysDialog)).toHaveCount(0);
});

test('Keys_do_nothing_in_a_dialog', async ({ mount, page }) => {
  await mountList(mount, page);
  await proveKeysAreLive(page);
  await page.getByTestId('open-modal').click();
  await expect(page.getByRole('dialog')).toBeVisible();

  await page.keyboard.press('j');
  await page.keyboard.press('x');
  await page.keyboard.press('5');

  await expect(log(page)).toHaveText('');
  await expect(page.getByTestId('focused-id')).toHaveText('id-SHIP-2');
  await expect(page.getByTestId('selected-count')).toHaveText('0');
});

test('Keys_do_nothing_outside_the_list', async ({ mount, page }) => {
  await mountList(mount, page);
  await proveKeysAreLive(page);
  await page.getByTestId('outside').focus();

  await page.keyboard.press('j');
  await page.keyboard.press('x');
  await page.keyboard.press('5');
  await page.keyboard.press('?');
  await page.keyboard.press('Escape');

  await expect(log(page)).toHaveText('');
  await expect(page.getByTestId('focused-id')).toHaveText('id-SHIP-2');
  await expect(page.getByTestId('selected-count')).toHaveText('0');
  await expect(page.getByTestId(ids.keysDialog)).toHaveCount(0);
});

test('Ctrl_P_still_opens_quick_search', async ({ mount, page }) => {
  await mountList(mount, page);
  await proveKeysAreLive(page);

  await page.keyboard.press('Control+p');

  await expect(page.getByTestId(quickSearchTestIds.dialog)).toBeVisible();
  await expect(log(page)).toHaveText('');
});

test('Alt_arrow_still_moves_the_row', async ({ mount, page }) => {
  // The real planning screen: PlanningRow takes Alt+Arrow and writes the move, with the list keys mounted beside it.
  const server = await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  // The keys are live on this screen: K goes from SHIP-172 to SHIP-171.
  await row(page, 'SHIP-172').focus();
  await page.keyboard.press('k');
  await expect(row(page, 'SHIP-171')).toBeFocused();
  expect(server.reorders).toHaveLength(0);

  await page.keyboard.press('Alt+ArrowUp');

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.reorders[0].url).toContain('/api/work-items/id-SHIP-171/reorder');
  expect(server.reorders[0].body).toEqual({ expectedRevision: 1, beforeItemId: 'id-SHIP-170' });
  expect(server.patches).toHaveLength(0);
});

test('M_then_2_runs_moveMenu_with_2', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();

  await page.keyboard.press('m');
  await expect(log(page)).toHaveText('moveMenu:undefined');
  await page.keyboard.press('2');
  await page.keyboard.press('m');
  await page.keyboard.press('t');
  await page.keyboard.press('m');
  await page.keyboard.press('b');

  await expect(log(page)).toHaveText(
    'moveMenu:undefined,moveMenu:2,moveMenu:undefined,moveMenu:t,moveMenu:undefined,moveMenu:b',
  );
});

test('M_then_0_cancels_the_hints', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();

  await page.keyboard.press('m');
  await page.keyboard.press('0');
  // The chord is over: a digit now starts an estimate.
  await page.keyboard.press('3');

  await expect(log(page)).toHaveText('moveMenu:undefined,moveMenu:cancel,estimate:3');
});

test('M_then_Esc_cancels_the_hints', async ({ mount, page }) => {
  await mountList(mount, page);
  await row(page, 'SHIP-1').focus();
  await page.keyboard.press('x');

  await page.keyboard.press('m');
  await page.keyboard.press('Escape');
  // The chord is over: a digit now starts an estimate, not a move.
  await page.keyboard.press('2');

  await expect(log(page)).toHaveText('moveMenu:undefined,moveMenu:cancel,estimate:2');
  // Esc only cancelled the chord; it did not clear the selection.
  await expect(page.getByTestId('selected-count')).toHaveText('1');
});

test('M_shows_the_sprint_hints_on_the_bar_and_Esc_clears_them', async ({ mount, page }) => {
  // The real planning screen: the bar's own moveMenu, not the harness fake.
  await stubBacklog(page, planningManyView());
  await mount(<PlanningMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  const hints = page.getByTestId(ids.bulkMoveSprint).locator('kbd');
  await row(page, 'SHIP-180').focus();
  await page.keyboard.press('x');
  await expect(page.getByTestId(ids.bulkBar)).toBeVisible();
  await expect(hints).toHaveCount(0);

  await page.keyboard.press('m');
  await expect(hints.first()).toHaveText('1');
  await expect(page.getByTestId(ids.bulkMoveTop).locator('kbd')).toHaveText('T');

  await page.keyboard.press('Escape');
  await expect(hints).toHaveCount(0);
  // Any other key ends the chord too.
  await page.keyboard.press('m');
  await expect(hints.first()).toBeVisible();
  await page.keyboard.press('z');
  await expect(hints).toHaveCount(0);
});

test('M_with_nothing_selected_shows_no_hints_and_does_not_error', async ({ mount, page }) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await stubBacklog(page, planningManyView());
  await mount(<PlanningMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  await row(page, 'SHIP-180').focus();
  await page.keyboard.press('m');
  await page.keyboard.press('Escape');

  await expect(page.getByTestId(ids.bulkBar)).toHaveCount(0);
  expect(errors).toEqual([]);
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
