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
