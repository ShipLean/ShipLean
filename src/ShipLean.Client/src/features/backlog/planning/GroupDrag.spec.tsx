import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';
import { dragRow } from '../ct/dragRow';
import { PlanningGroupHarness } from '../ct/PlanningGroupHarness';
import { PROJECT, planningManyView } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };
const rowOf = (page: Page, key: string) =>
  page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
const headerOf = (page: Page, title: string) =>
  page.getByTestId(ids.planningSection).filter({ hasText: title });
const select = async (page: Page, ...keys: string[]) => {
  for (const key of keys) await page.getByTestId(`harness-select-${key}`).click();
};

// Picks `row` up by its grip and holds the pointer over `target`.
async function holdOver(page: Page, row: Locator, target: Locator) {
  const grip = (await row.getByTestId(ids.grip).boundingBox())!;
  const box = (await target.boundingBox())!;
  const x = grip.x + grip.width / 2;
  const y = grip.y + grip.height / 2;
  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.mouse.move(x + 5, y + 10, { steps: 4 });
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 12 });
}

// Sprint 24: SHIP-160 (13 of 23). Sprint 25: empty. Backlog: SHIP-180 to SHIP-190, SHIP-181 is 5 points, SHIP-182 is 8.
async function mountPlanning(
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  page: Page,
  filtering = false,
) {
  const server = await stubBacklog(page, planningManyView());
  await mount(<PlanningGroupHarness project={PROJECT} filtering={filtering} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(12);
  return server;
}

test('Dragging_a_selected_row_moves_the_block_above_the_target', async ({ mount, page }) => {
  const server = await mountPlanning(mount, page);
  await select(page, 'SHIP-181', 'SHIP-183');

  await dragRow(page, rowOf(page, 'SHIP-181'), rowOf(page, 'SHIP-188'), 'above');

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => [i.id, i.placement])).toEqual([
    ['id-SHIP-181', { slot: 'before', beforeItemId: 'id-SHIP-188' }],
    ['id-SHIP-183', { slot: 'before', beforeItemId: 'id-SHIP-188' }],
  ]);
  expect(server.bulks[0].items.every((i) => i.changes === null)).toBe(true);
  expect(server.reorders).toHaveLength(0);
});

test('Block_keeps_rank_order_when_clicked_out_of_order', async ({ mount, page }) => {
  const server = await mountPlanning(mount, page);
  await select(page, 'SHIP-184', 'SHIP-181', 'SHIP-183');

  await dragRow(page, rowOf(page, 'SHIP-184'), rowOf(page, 'SHIP-188'), 'above');

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.id)).toEqual([
    'id-SHIP-181',
    'id-SHIP-183',
    'id-SHIP-184',
  ]);
});

test('Badge_shows_the_count', async ({ mount, page }) => {
  const server = await mountPlanning(mount, page);
  await select(page, 'SHIP-181', 'SHIP-183', 'SHIP-184');

  await holdOver(page, rowOf(page, 'SHIP-181'), rowOf(page, 'SHIP-188'));

  await expect(page.getByTestId(ids.dragBadge)).toHaveText('3 items');
  // The rest of the block dims in place.
  await expect(rowOf(page, 'SHIP-183')).toHaveCSS('opacity', '0.4');
  await expect(rowOf(page, 'SHIP-188')).toHaveCSS('opacity', '1');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.dragBadge)).toHaveCount(0);
  await page.mouse.up();
  expect(server.bulks).toHaveLength(0);
});

test('Sprint_header_previews_the_sum_while_over_it', async ({ mount, page }) => {
  const server = await mountPlanning(mount, page);
  await select(page, 'SHIP-181', 'SHIP-182');
  await expect(page.getByTestId(ids.sectionPreview)).toHaveCount(0);

  await holdOver(page, rowOf(page, 'SHIP-181'), headerOf(page, 'Sprint 24'));

  // 13 committed + 5 + 8 = 26 against a capacity of 23.
  const preview = page.getByTestId(ids.sectionPreview);
  await expect(preview).toHaveText('13 + 13 = 26 / 23');
  await expect(preview).toHaveAttribute('data-over', 'true');
  await expect(headerOf(page, 'Sprint 25').getByTestId(ids.sectionPreview)).toHaveCount(0);
  await page.keyboard.press('Escape');
  await page.mouse.up();
  await expect(preview).toHaveCount(0);
  expect(server.bulks).toHaveLength(0);
});

test('Dropping_on_a_header_moves_the_block_to_that_sprint', async ({ mount, page }) => {
  const server = await mountPlanning(mount, page);
  await select(page, 'SHIP-183', 'SHIP-181');

  await dragRow(page, rowOf(page, 'SHIP-181'), headerOf(page, 'Sprint 24'));

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => [i.id, i.placement, i.changes?.sprint])).toEqual([
    ['id-SHIP-181', null, { value: 'sp24' }],
    ['id-SHIP-183', null, { value: 'sp24' }],
  ]);
});

test('Dragging_an_unselected_row_moves_only_it', async ({ mount, page }) => {
  const server = await mountPlanning(mount, page);
  await select(page, 'SHIP-181', 'SHIP-183');

  await holdOver(page, rowOf(page, 'SHIP-184'), rowOf(page, 'SHIP-188'));
  // No badge and nothing dims: the selection is not coming along.
  await expect(page.getByTestId(ids.dragBadge)).toHaveCount(0);
  await expect(rowOf(page, 'SHIP-181')).toHaveCSS('opacity', '1');
  await page.mouse.up();

  await expect(page.getByTestId('harness-moves')).toHaveText('SHIP-184');
  expect(server.bulks).toHaveLength(0);
});

test('Drop_on_a_selected_row_does_nothing', async ({ mount, page }) => {
  const server = await mountPlanning(mount, page);
  await select(page, 'SHIP-181', 'SHIP-183');

  await holdOver(page, rowOf(page, 'SHIP-181'), rowOf(page, 'SHIP-183'));
  await expect(page.getByTestId(ids.dragBadge)).toBeVisible();
  // No line is drawn on a row that is part of the block.
  await expect(rowOf(page, 'SHIP-183')).not.toHaveAttribute('data-edge', /.+/);
  await page.mouse.up();

  await expect(page.getByTestId(ids.dragBadge)).toHaveCount(0);
  expect(server.bulks).toHaveLength(0);
  await expect(page.getByTestId('harness-moves')).toHaveText('');
  await expect(page.getByTestId('toast')).toHaveCount(0);
});

test('Dragging_is_off_when_filtering', async ({ mount, page }) => {
  const server = await mountPlanning(mount, page, true);
  await select(page, 'SHIP-181', 'SHIP-183');
  await expect(rowOf(page, 'SHIP-181').getByTestId(ids.grip)).toBeDisabled();

  await holdOver(page, rowOf(page, 'SHIP-181'), rowOf(page, 'SHIP-188'));
  await page.mouse.up();

  await expect(page.getByTestId(ids.dragBadge)).toHaveCount(0);
  expect(server.bulks).toHaveLength(0);
  await expect(page.getByTestId('harness-moves')).toHaveText('');
});
