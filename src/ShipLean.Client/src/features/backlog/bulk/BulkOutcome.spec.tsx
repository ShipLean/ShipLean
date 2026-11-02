import type { Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../../components/testIds';
import { backlogView, itemRow } from '../ct/fixtures';
import { BulkOutcomeHarness } from '../ct/BulkOutcomeHarness';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const ROWS = [
  itemRow({ key: 'SHIP-1', rank: 1, revision: 4 }),
  itemRow({ key: 'SHIP-2', rank: 2, revision: 5 }),
  itemRow({ key: 'SHIP-3', rank: 3, revision: 6 }),
];

const start = (page: Page) => stubBacklog(page, backlogView(ROWS));

async function ready(page: Page, server: { reads: string[] }) {
  await expect(page.getByTestId('select-all')).toBeVisible();
  await expect.poll(() => server.reads.length).toBeGreaterThan(0);
  await expect(page.getByTestId('selected-keys')).toBeAttached();
}

test('Stale_row_is_reported_and_the_others_moved', async ({ mount, page }) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  server.staleOnBulk(['id-SHIP-2']);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();

  await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
  await expect(page.getByTestId(ids.bulkChangedRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.bulkChangedRow)).toHaveAttribute('data-key', 'id-SHIP-2');
  expect(server.bulks[0].items.map((i) => i.id)).toEqual(['id-SHIP-1', 'id-SHIP-2', 'id-SHIP-3']);
  await expect(page.getByTestId('selected-keys')).toHaveText('SHIP-2');
  await expect(page.getByTestId(ids.undo)).toBeVisible();
});

test('Include_it_anyway_resends_at_the_current_revision', async ({ mount, page }) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  server.staleOnBulk(['id-SHIP-2']);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();
  await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
  server.staleOnBulk([]);

  await page.getByTestId(ids.bulkIncludeAnyway).click();

  await expect.poll(() => server.bulks.length).toBe(2);
  expect(server.bulks[1].items).toHaveLength(1);
  expect(server.bulks[1].items[0]).toMatchObject({ id: 'id-SHIP-2', expectedRevision: 6 });
  await expect(page.getByTestId(ids.bulkChanged)).toHaveCount(0);
});

test('Leave_it_clears_the_report', async ({ mount, page }) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  server.staleOnBulk(['id-SHIP-2']);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();

  await page.getByTestId(ids.bulkLeave).click();

  await expect(page.getByTestId(ids.bulkChanged)).toHaveCount(0);
  expect(server.bulks).toHaveLength(1);
});

test('Undo_sends_the_old_values', async ({ mount, page }) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();
  await expect(page.getByTestId(ids.undo)).toBeVisible();

  await page.getByTestId(ids.undo).click();

  await expect.poll(() => server.bulks.length).toBe(2);
  expect(
    server.bulks[1].items.map((i) => [i.id, i.expectedRevision, i.changes?.sprint, i.placement]),
  ).toEqual([
    ['id-SHIP-1', 5, { value: null }, null],
    ['id-SHIP-2', 6, { value: null }, null],
    ['id-SHIP-3', 7, { value: null }, null],
  ]);
});

test('Undo_of_a_row_changed_since_reports_it', async ({ mount, page }) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();
  await expect(page.getByTestId(ids.undo)).toBeVisible();
  server.staleOnBulk(['id-SHIP-3']);

  await page.getByTestId(ids.undo).click();

  await expect(page.getByTestId(ids.bulkChangedRow)).toHaveAttribute('data-key', 'id-SHIP-3');
  await expect(page.getByTestId(ids.bulkIncludeAnyway)).toHaveCount(0);
  expect(server.bulks[1].items.find((i) => i.id === 'id-SHIP-3')?.expectedRevision).toBe(7);
});

test('An_error_answer_changes_nothing', async ({ mount, page }) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  await page.route('**/api/projects/p1/backlog/bulk', (r) =>
    r.fulfill({
      status: 400,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Bad value', code: 'validation' }),
    }),
  );
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();

  await expect(page.getByTestId(componentTestIds.toast)).toBeVisible();
  await expect(page.getByTestId(ids.bulkChanged)).toHaveCount(0);
  await expect(page.getByTestId(ids.undo)).toHaveCount(0);
  expect(server.bulks).toHaveLength(0);
  await expect(page.getByTestId('selected-keys')).toHaveText('SHIP-1,SHIP-2,SHIP-3');
});

test('A_changed_row_selection_settles_and_can_be_cleared', async ({ mount, page }) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  server.staleOnBulk(['id-SHIP-2']);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();
  await expect(page.getByTestId('selected-keys')).toHaveText('SHIP-2');

  await page.getByTestId('clear-selection').click();

  await expect(page.getByTestId('selected-keys')).toHaveText('');
  await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
  expect(server.bulks).toHaveLength(1);
});

test('Undo_after_Include_it_anyway_puts_back_every_row', async ({ mount, page }) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  server.staleOnBulk(['id-SHIP-2']);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();
  await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
  server.staleOnBulk([]);
  await page.getByTestId(ids.bulkIncludeAnyway).click();
  await expect.poll(() => server.bulks.length).toBe(2);
  await expect(page.getByTestId(ids.bulkChanged)).toHaveCount(0);

  await page.getByTestId(ids.undo).click();

  await expect.poll(() => server.bulks.length).toBe(3);
  expect(server.bulks[2].items.map((i) => i.id).sort()).toEqual([
    'id-SHIP-1',
    'id-SHIP-2',
    'id-SHIP-3',
  ]);
  // SHIP-2 goes back to what the teammate left (Sprint 24 in the stub), the others to no sprint.
  expect(server.bulks[2].items.map((i) => i.changes?.sprint?.value)).toEqual([null, 'sp24', null]);
});

test('A_failed_Undo_is_offered_again', async ({ mount, page }) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();
  await expect(page.getByTestId(ids.undo)).toBeVisible();
  await page.route('**/api/projects/p1/backlog/bulk', (r) =>
    r.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Gone', code: 'not_found' }),
    }),
  );

  await page.getByTestId(ids.undo).click();

  await expect(page.getByTestId(componentTestIds.toast)).toBeVisible();
  await expect(page.getByTestId(ids.undo)).toBeVisible();
});

test('Undo_is_offered_once', async ({ mount, page }) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();
  await page.getByTestId(ids.undo).click();
  await expect.poll(() => server.bulks.length).toBe(2);
  await expect(page.getByTestId(ids.undo)).toHaveCount(0);

  await page.getByTestId('select-all').click();
  await page.getByTestId('run-points').click();
  await expect.poll(() => server.bulks.length).toBe(3);
  await expect(page.getByTestId(ids.undo)).toHaveCount(0);
});

test('Undo_after_Include_it_anyway_restores_the_value_the_teammate_left', async ({
  mount,
  page,
}) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  // The stub's conflict says the teammate had put SHIP-2 in Sprint 24; the rows here are in no sprint.
  server.staleOnBulk(['id-SHIP-2']);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();
  await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
  server.staleOnBulk([]);
  await page.getByTestId(ids.bulkIncludeAnyway).click();
  await expect.poll(() => server.bulks.length).toBe(2);

  await page.getByTestId(ids.undo).click();

  await expect.poll(() => server.bulks.length).toBe(3);
  expect(server.bulks[2].items.map((i) => [i.id, i.changes?.sprint])).toEqual([
    ['id-SHIP-1', { value: null }],
    ['id-SHIP-2', { value: 'sp24' }],
    ['id-SHIP-3', { value: null }],
  ]);
});

test('Undo_after_Include_it_anyway_puts_the_rows_back_in_their_old_order', async ({
  mount,
  page,
}) => {
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  server.staleOnBulk(['id-SHIP-2']);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-top').click();
  await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
  server.staleOnBulk([]);
  await page.getByTestId(ids.bulkIncludeAnyway).click();
  await expect.poll(() => server.bulks.length).toBe(2);

  await page.getByTestId(ids.undo).click();

  await expect.poll(() => server.bulks.length).toBe(3);
  expect(server.bulks[2].items.map((i) => [i.id, i.placement])).toEqual([
    ['id-SHIP-1', { slot: 'last', beforeItemId: null }],
    ['id-SHIP-2', { slot: 'last', beforeItemId: null }],
    ['id-SHIP-3', { slot: 'last', beforeItemId: null }],
  ]);
});

test('Include_it_anyway_gives_Undo_nine_seconds_again', async ({ mount, page }) => {
  await page.clock.install();
  const server = await start(page);
  await mount(<BulkOutcomeHarness />, route);
  await ready(page, server);
  server.staleOnBulk(['id-SHIP-2']);
  await page.getByTestId('select-all').click();
  await page.getByTestId('run-sprint').click();
  await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
  server.staleOnBulk([]);
  await page.clock.fastForward(8000);
  await expect(page.getByTestId(ids.undo)).toBeVisible();

  await page.getByTestId(ids.bulkIncludeAnyway).click();
  await expect.poll(() => server.bulks.length).toBe(2);
  await expect.poll(() => server.reads.length).toBeGreaterThan(2);
  await page.clock.fastForward(5000);
  await expect(page.getByTestId(ids.undo)).toBeVisible();

  await page.clock.fastForward(5000);
  await expect(page.getByTestId(ids.undo)).toHaveCount(0);
});
