import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { BacklogViewDto } from '../../../api/views';
import { backlogView, itemRow, PROJECT, review } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { backlogTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const ROWS = ['SHIP-1', 'SHIP-2', 'SHIP-3', 'SHIP-4'].map((key, i) =>
  itemRow({ key, rank: i + 1, points: null }),
);
const view = (flagged: string[], rows = ROWS): BacklogViewDto => ({
  ...backlogView(rows),
  review: review({ unestimated: flagged, no_parent: [ROWS[3].id] }),
});
const check = (page: Page, id: string) =>
  page.getByTestId(ids.planCheck).and(page.locator(`[data-check="${id}"]`));
const IDS = [ROWS[0].id, ROWS[1].id];

test('Strip_shows_five_counts', async ({ mount, page }) => {
  await stubBacklog(page, view(IDS));
  await mount(<ListMode project={PROJECT} />, route);

  await expect(page.getByTestId(ids.planCheck)).toHaveCount(5);
  await expect(check(page, 'unestimated')).toHaveText('2 not estimated');
  await expect(check(page, 'no_parent')).toHaveText('1 no parent');
  await expect(check(page, 'no_acceptance_criteria')).toHaveText('✓ no acceptance criteria');
  await expect(check(page, 'stale')).toHaveText('✓ stale');
  await expect(check(page, 'likely_duplicate')).toHaveText('✓ likely duplicates');
});

test('Clicking_a_count_shows_only_those_rows', async ({ mount, page }) => {
  await stubBacklog(page, view(IDS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(4);

  await check(page, 'unestimated').click();

  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  await expect(page.getByTestId(ids.planQueueBar)).toHaveText('0 of 2 fixed');
});

test('A_fixed_row_stays_in_the_queue_marked_done', async ({ mount, page }) => {
  const server = await stubBacklog(page, view(IDS));
  await mount(<ListMode project={PROJECT} />, route);
  await check(page, 'unestimated').click();
  await expect(page.getByTestId(ids.row)).toHaveCount(2);

  server.setBacklog(view([IDS[1]]));
  await page.getByTestId(ids.row).nth(0).getByTestId(ids.points).click();
  await page.getByTestId(ids.pointsInput).fill('5');
  await page.getByTestId(ids.pointsInput).press('Enter');

  await expect(page.getByTestId(ids.planQueueBar)).toHaveText('1 of 2 fixed');
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  const fixed = page.getByTestId(ids.row).nth(0);
  await expect(fixed.getByTestId(ids.queueDone)).toBeVisible();
  await expect(fixed.getByRole('img', { name: 'Fixed' })).toBeVisible();
  await expect(page.getByTestId(ids.row).nth(1).getByTestId(ids.queueDone)).toHaveCount(0);
  await expect(check(page, 'unestimated')).toHaveText('1 not estimated');
});

test('Esc_leaves_the_queue', async ({ mount, page }) => {
  await stubBacklog(page, view(IDS));
  await mount(<ListMode project={PROJECT} />, route);
  await check(page, 'unestimated').click();
  await expect(page.getByTestId(ids.row)).toHaveCount(2);

  await page.getByTestId(ids.row).nth(0).focus();
  await page.keyboard.press('Escape');

  await expect(page.getByTestId(ids.row)).toHaveCount(4);
  await expect(page.getByTestId(ids.planQueueBar)).toHaveCount(0);

  await check(page, 'unestimated').click();
  await page.getByTestId(ids.planQueueExit).click();
  await expect(page.getByTestId(ids.row)).toHaveCount(4);
});

test('The_queue_makes_the_mode_report_filtering', async ({ mount, page }) => {
  await stubBacklog(page, view(IDS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.grip).first()).toBeEnabled();

  await check(page, 'unestimated').click();

  await expect(page.getByTestId(ids.grip).first()).toBeDisabled();
});

test('No_strip_when_review_is_null', async ({ mount, page }) => {
  const server = await stubBacklog(page, view(IDS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.planStrip)).toBeVisible();

  server.setBacklog(backlogView(ROWS));
  await page.getByTestId(ids.row).nth(0).getByTestId(ids.points).click();
  await page.getByTestId(ids.pointsInput).fill('5');
  await page.getByTestId(ids.pointsInput).press('Enter');

  await expect(page.getByTestId(ids.planStrip)).toHaveCount(0);
  await expect(page.getByTestId(ids.row)).toHaveCount(4);
});

test('A_row_gone_from_the_list_is_dropped', async ({ mount, page }) => {
  const server = await stubBacklog(page, view(IDS));
  await mount(<ListMode project={PROJECT} />, route);
  await check(page, 'unestimated').click();
  await expect(page.getByTestId(ids.row)).toHaveCount(2);

  server.setBacklog(view([IDS[1]], ROWS.slice(1)));
  await page.getByTestId(ids.row).nth(1).getByTestId(ids.points).click();
  await page.getByTestId(ids.pointsInput).fill('5');
  await page.getByTestId(ids.pointsInput).press('Enter');

  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  await expect(page.getByTestId(ids.row).nth(0)).toHaveAttribute('data-key', 'SHIP-2');
  await expect(page.getByTestId(ids.planQueueBar)).toHaveText('0 of 1 fixed');
});
