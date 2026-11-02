import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';
import { PullLineHarness } from '../ct/PullLineHarness';
import { PROJECT, planningView } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };
const rowOf = (page: Page, key: string) =>
  page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));

// Backlog: SHIP-170 (5), SHIP-171 (8), SHIP-172 (3). Sprint 25: 8 of 22, so 5 + 8 fit and the 3 does not.
// Sprint 26: no capacity, velocity 21, so all three fit.
async function start(
  page: Page,
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  props: { forecast?: boolean; filtering?: boolean } = {},
  view = planningView(),
) {
  const server = await stubBacklog(page, view);
  await mount(<PullLineHarness project={PROJECT} {...props} />, route);
  await expect(page.getByTestId(ids.row).first()).toBeVisible();
  return server;
}

async function dragTo(page: Page, grip: Locator, row: Locator) {
  const from = (await grip.boundingBox())!;
  const to = (await row.boundingBox())!;
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  await page.mouse.move(from.x + 10, from.y + 30, { steps: 4 });
  await page.mouse.move(to.x + to.width / 2, to.y + to.height * 0.85, { steps: 12 });
  await page.mouse.up();
}

test('Line_sits_where_the_next_sprint_fills', async ({ mount, page }) => {
  await start(page, mount);

  const line = page.getByTestId(ids.pullLine);
  await expect(line).toHaveCount(1);
  const above = (await rowOf(page, 'SHIP-171').boundingBox())!;
  const below = (await rowOf(page, 'SHIP-172').boundingBox())!;
  const at = (await line.boundingBox())!;
  expect(at.y).toBeGreaterThan(above.y);
  expect(at.y + at.height).toBeLessThanOrEqual(below.y + 1);
  await expect(page.getByTestId(ids.pullTarget)).toHaveValue('sp25');
  await expect(page.getByTestId(ids.pullSum)).toHaveText('8 + 13 = 21 / 22');
  await expect(page.getByTestId(ids.pullGo)).toHaveText('Pull into Sprint 25');
  await expect(page.getByTestId(ids.pullReset)).toHaveCount(0);
});

test('Pull_moves_the_rows_above_the_line', async ({ mount, page }) => {
  const server = await start(page, mount);

  await page.getByTestId(ids.pullGo).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => [i.id, i.placement])).toEqual([
    ['id-SHIP-170', null],
    ['id-SHIP-171', null],
  ]);
  expect(server.bulks[0].items.every((i) => i.changes?.sprint?.value === 'sp25')).toBe(true);
});

test('Drag_changes_the_sum_as_it_moves', async ({ mount, page }) => {
  const server = await start(page, mount);

  await dragTo(page, page.getByTestId(ids.pullGrip), rowOf(page, 'SHIP-172'));

  await expect(page.getByTestId(ids.pullSum)).toContainText('8 + 16 = 24 / 22');
  await expect(page.getByTestId(ids.pullReset)).toBeVisible();
  expect(server.bulks).toHaveLength(0);
});

test('Going_over_warns_and_still_pulls', async ({ mount, page }) => {
  const server = await start(page, mount);
  await dragTo(page, page.getByTestId(ids.pullGrip), rowOf(page, 'SHIP-172'));

  const sum = page.getByTestId(ids.pullSum);
  await expect(sum).toHaveAttribute('data-over', 'true');
  await expect(sum).toContainText('+2 over');
  await expect(page.getByTestId(ids.pullGo)).toBeEnabled();
  await page.getByTestId(ids.pullGo).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.id)).toEqual([
    'id-SHIP-170',
    'id-SHIP-171',
    'id-SHIP-172',
  ]);
});

test('Alt_arrow_moves_the_line_one_row', async ({ mount, page }) => {
  await start(page, mount);
  const grip = page.getByTestId(ids.pullGrip);
  const sum = page.getByTestId(ids.pullSum);

  await grip.focus();
  await page.keyboard.press('Alt+ArrowDown');
  await expect(sum).toContainText('8 + 16 = 24 / 22');
  await expect(grip).toBeFocused();
  await page.keyboard.press('Alt+ArrowUp');
  await page.keyboard.press('Alt+ArrowUp');
  await expect(sum).toHaveText('8 + 5 = 13 / 22');
  await expect(grip).toBeFocused();
});

test('Back_to_what_fits_resets_it', async ({ mount, page }) => {
  await start(page, mount);
  const sum = page.getByTestId(ids.pullSum);
  await expect(page.getByTestId(ids.pullReset)).toHaveCount(0);
  await page.getByTestId(ids.pullGrip).focus();
  await page.keyboard.press('Alt+ArrowUp');
  await expect(sum).toHaveText('8 + 5 = 13 / 22');

  await page.getByTestId(ids.pullReset).click();

  await expect(sum).toHaveText('8 + 13 = 21 / 22');
  await expect(page.getByTestId(ids.pullReset)).toHaveCount(0);
});

test('Choosing_another_sprint_recuts', async ({ mount, page }) => {
  const server = await start(page, mount);

  await page.getByTestId(ids.pullTarget).selectOption('sp26');

  await expect(page.getByTestId(ids.pullSum)).toHaveText('0 + 16 = 16 / 21');
  await expect(page.getByTestId(ids.pullGo)).toHaveText('Pull into Sprint 26');
  await expect(page.getByTestId(ids.pullLine)).toHaveCount(1);
  await page.getByTestId(ids.pullGo).click();
  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items).toHaveLength(3);
  expect(server.bulks[0].items.every((i) => i.changes?.sprint?.value === 'sp26')).toBe(true);
});

test('No_future_sprint_shows_no_line', async ({ mount, page }) => {
  const view = planningView();
  view.sections = view.sections.filter((s) => s.sprint === null || s.sprint.status === 'current');
  await start(page, mount, {}, view);

  await expect(page.getByTestId(ids.row)).toHaveCount(4);
  await expect(page.getByTestId(ids.pullLine)).toHaveCount(0);
});

test('Hidden_when_filtering', async ({ mount, page }) => {
  await start(page, mount, { filtering: true });
  await expect(page.getByTestId(ids.pullLine)).toHaveCount(0);
});

test('Hidden_with_forecast_off', async ({ mount, page }) => {
  await start(page, mount, { forecast: false });
  await expect(page.getByTestId(ids.pullLine)).toHaveCount(0);
});

test('A_stale_row_lands_in_the_runner_outcome', async ({ mount, page }) => {
  const server = await start(page, mount);
  server.staleOnBulk(['id-SHIP-171']);

  await page.getByTestId(ids.pullGo).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.id)).toEqual(['id-SHIP-170', 'id-SHIP-171']);
  await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
  await expect(page.getByTestId(ids.bulkChangedRow)).toHaveCount(1);
});
