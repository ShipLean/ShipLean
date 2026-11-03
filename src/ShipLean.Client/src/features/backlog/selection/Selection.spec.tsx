import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { backlogView, itemRow, planningManyView, PROJECT, ROWS } from '../ct/fixtures';
import { SelectionCommandHarness } from '../ct/SelectionCommandHarness';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { PlanningMode } from '../planning/PlanningMode';
import { backlogTestIds as ids } from '../testIds';

const planning = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };
const list = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const row = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.row}"][data-key="${key}"]`);
const box = (page: Page, key: string) => row(page, key).getByRole('checkbox');

test('Checkbox_selects_without_opening_the_item', async ({ mount, page }) => {
  await stubBacklog(page, planningManyView());
  await mount(<PlanningMode project={PROJECT} />, planning);

  await box(page, 'SHIP-180').click();

  await expect(row(page, 'SHIP-180')).toHaveAttribute('aria-selected', 'true');
  await expect(box(page, 'SHIP-180')).toBeChecked();
  await expect(box(page, 'SHIP-180')).toHaveAccessibleName('Select SHIP-180');
  await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('Ctrl_click_on_row_toggles', async ({ mount, page }) => {
  await stubBacklog(page, planningManyView());
  await mount(<PlanningMode project={PROJECT} />, planning);

  await row(page, 'SHIP-181')
    .getByTestId(ids.rank)
    .click({ modifiers: ['Control'] });
  await row(page, 'SHIP-183')
    .getByTestId(ids.rank)
    .click({ modifiers: ['Control'] });
  await expect(page.getByTestId(ids.row).and(page.locator('[aria-selected="true"]'))).toHaveCount(
    2,
  );

  await row(page, 'SHIP-181')
    .getByTestId(ids.rank)
    .click({ modifiers: ['Control'] });
  await expect(row(page, 'SHIP-181')).toHaveAttribute('aria-selected', 'false');
  await expect(row(page, 'SHIP-183')).toHaveAttribute('aria-selected', 'true');
});

test('Shift_click_selects_a_range_across_sections', async ({ mount, page }) => {
  await stubBacklog(page, planningManyView());
  await mount(<PlanningMode project={PROJECT} />, planning);

  // SHIP-160 is in Sprint 24, above the Backlog section holding the rest.
  await box(page, 'SHIP-160').click();
  await box(page, 'SHIP-182').click({ modifiers: ['Shift'] });

  for (const key of ['SHIP-160', 'SHIP-180', 'SHIP-181', 'SHIP-182'])
    await expect(row(page, key)).toHaveAttribute('aria-selected', 'true');
  await expect(row(page, 'SHIP-183')).toHaveAttribute('aria-selected', 'false');

  // Shift on the row, not the box, extends the same way.
  await row(page, 'SHIP-185')
    .getByTestId(ids.rank)
    .click({ modifiers: ['Shift'] });
  await expect(row(page, 'SHIP-184')).toHaveAttribute('aria-selected', 'true');
  await expect(row(page, 'SHIP-186')).toHaveAttribute('aria-selected', 'false');
});

// The head box is the one select-all; it takes every shown row, whichever section they are in, and clears them again.
test('Select_all_in_a_section_then_another', async ({ mount, page }) => {
  await stubBacklog(page, planningManyView());
  await mount(<PlanningMode project={PROJECT} />, planning);
  const head = page.getByTestId(ids.selectAll).getByRole('checkbox');

  await box(page, 'SHIP-160').click();
  await expect(head).not.toBeChecked();
  expect(await head.evaluate((input: HTMLInputElement) => input.indeterminate)).toBe(true);

  await head.click();
  await expect(page.getByTestId(ids.row).and(page.locator('[aria-selected="true"]'))).toHaveCount(
    12,
  );
  await expect(head).toBeChecked();

  await head.click();
  await expect(page.getByTestId(ids.row).and(page.locator('[aria-selected="true"]'))).toHaveCount(
    0,
  );
});

test('Plain_click_moves_focus_and_keeps_the_selection', async ({ mount, page }) => {
  await stubBacklog(page, planningManyView());
  await mount(<PlanningMode project={PROJECT} />, planning);

  await box(page, 'SHIP-180').click();
  await box(page, 'SHIP-181').click();
  await row(page, 'SHIP-185').getByTestId(ids.rank).click();

  await expect(row(page, 'SHIP-185')).toBeFocused();
  await expect(row(page, 'SHIP-185')).toHaveAttribute('data-focused', 'true');
  await expect(row(page, 'SHIP-185')).toHaveAttribute('aria-selected', 'false');
  await expect(row(page, 'SHIP-180')).toHaveAttribute('aria-selected', 'true');
  await expect(row(page, 'SHIP-181')).toHaveAttribute('aria-selected', 'true');
});

test('Ctrl_click_on_title_selects_while_a_selection_exists', async ({ mount, page }) => {
  await stubBacklog(page, planningManyView());
  await mount(<PlanningMode project={PROJECT} />, planning);

  await box(page, 'SHIP-180').click();
  const popups: unknown[] = [];
  page.context().on('page', (opened) => popups.push(opened));
  await row(page, 'SHIP-182')
    .getByTestId(ids.title)
    .click({ modifiers: ['Control'] });

  await expect(row(page, 'SHIP-182')).toHaveAttribute('aria-selected', 'true');
  await expect(row(page, 'SHIP-180')).toHaveAttribute('aria-selected', 'true');
  expect(popups).toHaveLength(0);
  await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('Ctrl_click_on_title_without_a_selection_keeps_the_new_tab_href', async ({ mount, page }) => {
  await stubBacklog(page, planningManyView());
  await mount(<PlanningMode project={PROJECT} />, planning);

  const title = row(page, 'SHIP-182').getByTestId(ids.title);
  await expect(title).toHaveAttribute('href', '/p/SHIP/items/SHIP-182');
  // The click is not taken: the link is left to the browser, which opens a tab.
  const defaultPrevented = await title.evaluate((link: Element) => {
    const event = new MouseEvent('click', { bubbles: true, cancelable: true, ctrlKey: true });
    link.dispatchEvent(event);
    return event.defaultPrevented;
  });
  expect(defaultPrevented).toBe(false);
  await expect(row(page, 'SHIP-182')).toHaveAttribute('aria-selected', 'false');
});

test('A_row_that_left_is_dropped_from_the_selection', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, list);

  await box(page, 'SHIP-160').click();
  await box(page, 'SHIP-158').click();
  await expect(box(page, 'SHIP-158')).toBeChecked();

  // The next read no longer has SHIP-160 (a teammate moved it); SHIP-158 stays selected through the refetch.
  server.setBacklog(backlogView([itemRow({ key: 'SHIP-184', rank: 1 }), ROWS[1]]));
  await page.getByTestId(ids.newItem).click();
  await page.getByTestId(ids.addTitle).fill('Another');
  await page.getByTestId(ids.addTitle).press('Enter');

  await expect(row(page, 'SHIP-184')).toBeVisible();
  await expect(row(page, 'SHIP-160')).toHaveCount(0);
  await expect(box(page, 'SHIP-158')).toBeChecked();
  await expect(page.getByTestId(ids.row).and(page.locator('[aria-selected="true"]'))).toHaveCount(
    1,
  );
});

test('Over_100_selected_is_allowed', async ({ mount, page }) => {
  const rows = Array.from({ length: 120 }, (_, i) => itemRow({ key: `SHIP-${300 + i}`, rank: i }));
  await stubBacklog(page, backlogView(rows));
  await mount(<ListMode project={PROJECT} />, list);

  await page.getByTestId(ids.selectAll).getByRole('checkbox').click();

  await expect(page.getByTestId(ids.row).and(page.locator('[aria-selected="true"]'))).toHaveCount(
    120,
  );
});

test('Commands_toggleSelect_and_clear_run_by_name', async ({ mount, page }) => {
  await mount(<SelectionCommandHarness />);
  const count = page.getByTestId('selected-count');

  await page.getByTestId('command-arg').fill('id-SHIP-2');
  await page.getByTestId('run-command-toggleSelect').click();
  await expect(count).toHaveText('1');

  await page.getByTestId('command-arg').fill('');
  await page.getByTestId('run-command-clear').click();
  await expect(count).toHaveText('0');

  // With no argument it toggles the focused row.
  await page.getByTestId('harness-row').nth(0).getByRole('checkbox').click();
  await page.getByTestId('harness-row').nth(0).getByRole('checkbox').click();
  await page.getByTestId('run-command-toggleSelect').click();
  await expect(count).toHaveText('1');
});
