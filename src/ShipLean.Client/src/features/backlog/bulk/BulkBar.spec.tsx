import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { StateDto } from '../../../api/projects';
import type { BacklogViewDto } from '../../../api/views';
import { ToastHost } from '../../../components/ToastHost';
import { bug, story } from '../../workItem/ct/fixtures';
import { backlogView, itemRow, PROJECT, planningManyView } from '../ct/fixtures';
import { BulkBarHarness } from '../ct/BulkBarHarness';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { PlanningMode } from '../planning/PlanningMode';
import { backlogTestIds as ids } from '../testIds';

const planning = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };
const list = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const box = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.row}"][data-key="${key}"]`).getByRole('checkbox');
const select = async (page: Page, ...keys: string[]) => {
  for (const key of keys) await box(page, key).click();
};

const state = (id: string, type: StateDto['type'], name: string, order: number): StateDto => ({
  id,
  type,
  name,
  category: 'inProgress',
  order,
  itemCount: 0,
});
const STATES: StateDto[] = [
  state('s-active', 'story', 'Active', 1),
  state('s-review', 'story', 'In review', 2),
  { ...state('s-gone', 'story', 'Removed', 9), category: 'removed' },
  state('b-active', 'bug', 'Active', 1),
  { ...state('b-gone', 'bug', 'Removed', 9), category: 'removed' },
];
const feature = {
  ...story,
  id: 'f1',
  key: 'SHIP-10',
  title: 'Checkout',
  type: { id: 'feature', name: 'Feature' },
};
const epic = {
  ...bug,
  id: 'e1',
  key: 'SHIP-1',
  title: 'An epic',
  type: { id: 'epic', name: 'Epic' },
};

async function open(
  mount: (node: React.JSX.Element, options?: object) => Promise<unknown>,
  page: Page,
  view: BacklogViewDto = planningManyView(),
) {
  const server = await stubBacklog(page, view);
  await page.route('**/api/projects/p1/states', (route) =>
    route.fulfill({ contentType: 'application/json', body: JSON.stringify(STATES) }),
  );
  await page.route('**/api/projects/p1/work-items?*', (route) => {
    const type = new URL(route.request().url()).searchParams.get('type');
    const items = type === 'feature' ? [feature] : type === 'epic' ? [epic] : [];
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify(items) });
  });
  await mount(
    <>
      <PlanningMode project={PROJECT} />
      <ToastHost />
    </>,
    planning,
  );
  return server;
}

const sprintButton = (page: Page, name: string) =>
  page.getByTestId(ids.bulkMoveSprint).filter({ hasText: name });

test('Move_to_sprint_sends_one_bulk_with_the_new_sprint', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-181');

  await sprintButton(page, 'Sprint 25').click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.id)).toEqual(['id-SHIP-180', 'id-SHIP-181']);
  expect(server.bulks[0].items.map((i) => i.changes?.sprint)).toEqual([
    { value: 'sp25' },
    { value: 'sp25' },
  ]);
  await expect(page.getByTestId(ids.bulkBar)).toHaveCount(0);
});

test('Sprint_button_shows_13_plus_8', async ({ mount, page }) => {
  await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-181');

  await expect(sprintButton(page, 'Sprint 24').getByTestId(ids.bulkSum)).toHaveText(
    '13 + 8 = 21 / 23',
  );
});

test('Over_capacity_warns_and_still_moves', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-181', 'SHIP-182');

  const sum = sprintButton(page, 'Sprint 24').getByTestId(ids.bulkSum);
  await expect(sum).toHaveText('13 + 16 = 29 / 23 · +6 over');
  await expect(sum).toHaveAttribute('data-over', 'true');
  await sprintButton(page, 'Sprint 24').click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items).toHaveLength(3);
});

test('Move_to_top_keeps_rank_order', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-183', 'SHIP-181');

  await page.getByTestId(ids.bulkMoveTop).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.id)).toEqual(['id-SHIP-181', 'id-SHIP-183']);
  expect(server.bulks[0].items.map((i) => i.placement)).toEqual([
    { slot: 'first', beforeItemId: null },
    { slot: 'first', beforeItemId: null },
  ]);
  expect(server.bulks[0].items.every((i) => i.changes === null)).toBe(true);
});

test('Top_in_Planning_clears_the_sprint_of_a_row_in_one', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-160', 'SHIP-181');

  await page.getByTestId(ids.bulkMoveTop).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => [i.id, i.changes?.sprint ?? null, i.placement])).toEqual([
    ['id-SHIP-160', { value: null }, { slot: 'first', beforeItemId: null }],
    ['id-SHIP-181', null, { slot: 'first', beforeItemId: null }],
  ]);
  await expect(page.getByText('2 moved to the top of the backlog')).toBeVisible();
});

test('Top_in_List_keeps_the_sprint', async ({ mount, page }) => {
  const server = await stubBacklog(
    page,
    backlogView([
      itemRow({
        key: 'SHIP-160',
        rank: 1,
        sprint: { id: 'sp24', name: 'Sprint 24', status: 'current' },
      }),
    ]),
  );
  await page.route('**/api/projects/p1/states', (route) => route.fulfill({ body: '[]' }));
  await mount(<ListMode project={PROJECT} />, list);
  await select(page, 'SHIP-160');

  await page.getByTestId(ids.bulkMoveTop).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items[0].changes).toBeNull();
  expect(server.bulks[0].items[0].placement).toEqual({ slot: 'first', beforeItemId: null });
});

test('Set_points_on_all_selected', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-181');

  await page.getByTestId(ids.bulkSetPoints).click();
  await page.getByTestId(ids.bulkPointsInput).fill('8');
  await page.getByTestId(ids.bulkPointsInput).press('Enter');

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.changes?.points)).toEqual([{ value: 8 }, { value: 8 }]);
  await expect(page.getByTestId(ids.bulkBar)).toBeVisible();
  await expect(page.getByTestId(ids.bulkSetPoints)).toBeFocused();
});

test('An_empty_points_field_clears_the_estimate', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180');

  await page.getByTestId(ids.bulkSetPoints).click();
  await page.getByTestId(ids.bulkPointsInput).press('Enter');

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items[0].changes?.points).toEqual({ value: null });
});

test('Set_state_resolves_per_type', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-182');

  await page.getByTestId(ids.bulkSetState).click();
  await page.getByTestId(ids.bulkMenuOption).filter({ hasText: 'Active' }).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => [i.id, i.changes?.state])).toEqual([
    ['id-SHIP-180', { value: 's-active' }],
    ['id-SHIP-182', { value: 'b-active' }],
  ]);
});

test('Bug_without_that_state_is_left_out_and_counted', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-181', 'SHIP-182');

  await page.getByTestId(ids.bulkSetState).click();
  await page.getByTestId(ids.bulkMenuOption).filter({ hasText: 'In review' }).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.id)).toEqual(['id-SHIP-180', 'id-SHIP-181']);
  await expect(page.getByText('1 bug has no In review')).toBeVisible();
});

test('Set_tag_keeps_each_rows_tags', async ({ mount, page }) => {
  const view = planningManyView();
  const tagged = view.sections[2].rows[0];
  tagged.tags = [{ name: 'api', colour: null }];
  const server = await open(mount, page, view);
  await select(page, 'SHIP-180', 'SHIP-181');

  await page.getByTestId(ids.bulkSetTag).click();
  await page.getByTestId(ids.bulkTagInput).fill('urgent');
  await page.getByTestId(ids.bulkTagInput).press('Enter');

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.changes?.tags)).toEqual([
    { value: ['api', 'urgent'] },
    { value: ['urgent'] },
  ]);
});

test('Set_parent_offers_only_allowed_parents', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-182');

  await page.getByTestId(ids.bulkSetParent).click();

  await expect(page.getByText('Checkout')).toBeVisible();
  await expect(page.getByText('An epic')).toHaveCount(0);
  await page.getByText('Checkout').click();
  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.changes?.parent)).toEqual([
    { value: 'f1' },
    { value: 'f1' },
  ]);
});

test('Remove_clears_the_selection', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-182');

  await page.getByTestId(ids.bulkRemove).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.changes?.state)).toEqual([
    { value: 's-gone' },
    { value: 'b-gone' },
  ]);
  await expect(page.getByTestId(ids.bulkBar)).toHaveCount(0);
});

test('Over_100_turns_actions_off', async ({ mount, page }) => {
  const base = planningManyView();
  const extra = Array.from({ length: 101 }, (_, i) =>
    itemRow({ key: `SHIP-${300 + i}`, rank: 20 + i }),
  );
  const view = {
    ...base,
    sections: base.sections.map((section) =>
      section.id === 'backlog' ? { ...section, rows: [...section.rows, ...extra] } : section,
    ),
  };
  await open(mount, page, view);

  await page.getByTestId(ids.selectAll).getByRole('checkbox').click();

  await expect(page.getByTestId(ids.bulkBar)).toBeVisible();
  await expect(page.getByTestId(ids.bulkLimit)).toContainText('100');
  for (const id of [
    ids.bulkMoveTop,
    ids.bulkMoveBottom,
    ids.bulkSetPoints,
    ids.bulkSetState,
    ids.bulkSetParent,
    ids.bulkSetTag,
    ids.bulkRemove,
  ])
    await expect(page.getByTestId(id)).toBeDisabled();
  const sprintButtons = page.getByTestId(ids.bulkMoveSprint);
  expect(await sprintButtons.count()).toBeGreaterThan(0);
  for (const button of await sprintButtons.all()) await expect(button).toBeDisabled();
  await expect(page.getByTestId(ids.bulkClear)).toBeEnabled();
});

test('There_is_no_assign_action', async ({ mount, page }) => {
  await open(mount, page);
  await select(page, 'SHIP-180');

  await expect(page.getByTestId(ids.bulkBar)).toBeVisible();
  await expect(page.getByTestId(ids.bulkBar).getByText(/assign/i)).toHaveCount(0);
  await expect(page.getByTestId(ids.bulkBar).getByRole('button', { name: /assign/i })).toHaveCount(
    0,
  );
});

test('MoveMenu_command_moves_by_sprint_number_top_and_bottom', async ({ mount, page }) => {
  const server = await stubBacklog(page, planningManyView());
  await page.route('**/api/projects/p1/states', (route) => route.fulfill({ body: '[]' }));
  const view = planningManyView();
  await mount(<BulkBarHarness view={view} selected={['id-SHIP-160']} />);
  const run = async (arg: string) => {
    await page.getByTestId('command-arg').fill(arg);
    await page.getByTestId('run-command-moveMenu').click();
  };

  // A move clears the selection, so each one selects again first.
  const reselect = () => page.getByTestId('harness-reselect').click();

  await run('2');
  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items[0].changes?.sprint).toEqual({ value: 'sp25' });

  await reselect();
  await run('b');
  await expect.poll(() => server.bulks.length).toBe(2);
  expect(server.bulks[1].items[0].placement).toEqual({ slot: 'last', beforeItemId: null });

  await reselect();
  await run('t');
  await expect.poll(() => server.bulks.length).toBe(3);
  expect(server.bulks[2].items[0].placement).toEqual({ slot: 'first', beforeItemId: null });
});

test('Remove_with_a_stale_row_leaves_that_row_selected', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-182');
  server.staleOnBulk(['id-SHIP-182']);

  await page.getByTestId(ids.bulkRemove).click();

  await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
  await expect(page.getByTestId(ids.bulkCount)).toHaveText('1 selected');
  await expect(box(page, 'SHIP-182')).toBeChecked();
  await expect(box(page, 'SHIP-180')).not.toBeChecked();
});

test('A_failed_Remove_keeps_the_selection', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-182');
  await page.route('**/api/projects/p1/backlog/bulk', (route) =>
    route.fulfill({ status: 500, body: '{}' }),
  );

  await page.getByTestId(ids.bulkRemove).click();

  await expect(page.getByTestId(ids.bulkCount)).toHaveText('2 selected');
  expect(server.bulks).toHaveLength(0);
});

test('Focus_returns_to_the_button_after_an_outside_click_closes_a_menu', async ({
  mount,
  page,
}) => {
  await open(mount, page);
  await select(page, 'SHIP-180');
  await page.getByTestId(ids.bulkSetPoints).click();
  await expect(page.getByTestId(ids.bulkMenu)).toBeVisible();

  await page.getByTestId(ids.bulkCount).click();

  await expect(page.getByTestId(ids.bulkMenu)).toHaveCount(0);
  await expect(page.getByTestId(ids.bulkSetPoints)).toBeFocused();
});

test('Move_to_a_sprint_clears_the_selection', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-181');

  await sprintButton(page, 'Sprint 25').click();

  await expect.poll(() => server.bulks.length).toBe(1);
  await expect(page.getByTestId(ids.bulkBar)).toHaveCount(0);
  await expect(box(page, 'SHIP-180')).not.toBeChecked();
  await expect(box(page, 'SHIP-181')).not.toBeChecked();
});

test('Move_to_top_clears_the_selection', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-181', 'SHIP-183');

  await page.getByTestId(ids.bulkMoveTop).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  await expect(page.getByTestId(ids.bulkBar)).toHaveCount(0);
});

test('Move_to_bottom_clears_the_selection', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-181', 'SHIP-183');

  await page.getByTestId(ids.bulkMoveBottom).click();

  await expect.poll(() => server.bulks.length).toBe(1);
  await expect(page.getByTestId(ids.bulkBar)).toHaveCount(0);
});

test('A_failed_move_keeps_the_selection', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-181');
  await page.route('**/api/projects/p1/backlog/bulk', (route) =>
    route.fulfill({ status: 500, body: '{}' }),
  );

  await sprintButton(page, 'Sprint 25').click();

  await expect(page.getByTestId(ids.bulkCount)).toHaveText('2 selected');
  expect(server.bulks).toHaveLength(0);
});

test('Move_with_a_stale_row_leaves_only_that_row_selected', async ({ mount, page }) => {
  const server = await open(mount, page);
  await select(page, 'SHIP-180', 'SHIP-181', 'SHIP-182');
  server.staleOnBulk(['id-SHIP-182']);

  await sprintButton(page, 'Sprint 25').click();

  await expect(page.getByTestId(ids.bulkChanged)).toBeVisible();
  await expect(page.getByTestId(ids.bulkCount)).toHaveText('1 selected');
  await expect(box(page, 'SHIP-182')).toBeChecked();
  await expect(box(page, 'SHIP-180')).not.toBeChecked();
});
