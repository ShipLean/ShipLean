import { expect, test } from '@playwright/experimental-ct-react';
import { SearchHarness } from '../ct/SearchHarness';
import { backlogView, PROJECT, planningView, ROWS } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { PlanningMode } from '../planning/PlanningMode';
import { backlogTestIds as ids } from '../testIds';

const listRoute = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const planningRoute = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };

test('Search_narrows_list_by_title_and_key', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, listRoute);
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  const reads = server.reads.length;

  await page.getByTestId(ids.searchBox).fill('BIOMETRIC');
  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  await expect(page.getByTestId(ids.row)).toHaveAttribute('data-key', 'SHIP-160');

  await page.getByTestId(ids.searchBox).fill('ship-158');
  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  await expect(page.getByTestId(ids.row)).toHaveAttribute('data-key', 'SHIP-158');

  await page.getByTestId(ids.searchBox).fill('zzz');
  await expect(page.getByTestId(ids.noRows)).toHaveText('Nothing matches "zzz"');
  // The rows are the ones already loaded: no request.
  expect(server.reads).toHaveLength(reads);
});

test('Search_narrows_planning_sections', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, planningRoute);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await page.getByTestId(ids.searchBox).fill('SHIP-158');
  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  await expect(page.getByTestId(ids.row)).toHaveAttribute('data-key', 'SHIP-158');
  await expect(page.getByTestId(ids.planningSectionTitle)).toHaveCount(4);

  await page.getByTestId(ids.searchBox).fill('zzz');
  await expect(page.getByTestId(ids.row)).toHaveCount(0);
  await expect(page.getByTestId(ids.noRows)).toHaveText('Nothing matches "zzz"');
});

test('Search_makes_the_mode_report_filtering', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, listRoute);
  await expect(page.getByTestId(ids.dragOffBanner)).toHaveCount(0);
  await expect(page.getByTestId(ids.grip).first()).toBeEnabled();
  await page.getByTestId(ids.searchBox).fill('SHIP');
  await expect(page.getByTestId(ids.dragOffBanner)).toContainText('Reordering is off');
  await expect(page.getByTestId(ids.grip).first()).toBeDisabled();
  await page.getByTestId(ids.searchBox).fill('');
  await expect(page.getByTestId(ids.dragOffBanner)).toHaveCount(0);
});

test('Search_makes_planning_report_filtering', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, planningRoute);
  await expect(page.getByTestId(ids.dragOffBanner)).toHaveCount(0);
  await page.getByTestId(ids.searchBox).fill('SHIP');
  await expect(page.getByTestId(ids.dragOffBanner)).toHaveText(
    "Dragging and the pull line are off while a search or check is on, because rank is relative to rows you can't see. Moving to a sprint, the top or the bottom still works.",
  );
});

test('Esc_clears_and_refocuses', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, listRoute);
  const box = page.getByTestId(ids.searchBox);
  await box.fill('biometric');
  await expect(page.getByTestId(ids.row)).toHaveCount(1);

  await box.press('Escape');

  await expect(box).toHaveValue('');
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  await expect(page.getByTestId(ids.row).first()).toBeFocused();
});

test('Search_command_focuses_the_box', async ({ mount, page }) => {
  await mount(<SearchHarness />);
  await expect(page.getByTestId(ids.searchBox)).not.toBeFocused();
  await page.getByTestId('run-command-search').click();
  await expect(page.getByTestId(ids.searchBox)).toBeFocused();
});
