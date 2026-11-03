import { expect, test } from '@playwright/experimental-ct-react';
import { ListMode } from '../list/ListMode';
import type { BacklogAddRequest } from '../../../api/backlogWrites';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';
import { ToastHost } from '../../../components/ToastHost';
import { AddHereHarness } from '../ct/AddHereHarness';
import { backlogView, itemRow, planningView, PROJECT } from '../ct/fixtures';
import { stubBacklog, type BacklogServer } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const ROWS = ['SHIP-1', 'SHIP-2', 'SHIP-3'].map((key, i) => itemRow({ key, rank: i + 1 }));

interface Page {
  route(url: string, handler: (route: Route) => Promise<void>): Promise<unknown>;
}
interface Route {
  fulfill(response: { status: number; contentType?: string; body?: string }): Promise<void>;
  request(): { postDataJSON(): unknown };
}
const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

// Answers a parent picker with one Feature.
async function stubFeatures(page: Page) {
  await page.route('**/api/projects/p1/work-items?*', (route) =>
    route.fulfill(
      json([
        {
          id: 'f1',
          key: 'SHIP-9',
          title: 'Mobile sign-in',
          type: { id: 'feature', name: 'Feature' },
          parent: null,
        },
      ]),
    ),
  );
}

// A create that really adds the row, where the request says: first in its sprint, before the named row, else last.
// `hold` keeps the first answer back until the test lets it go.
async function stubCreates(
  page: Page,
  server: BacklogServer,
  view: BacklogViewDto,
  hold?: Promise<void>,
) {
  let current = view;
  let made = 0;
  await page.route('**/api/projects/p1/backlog/items', async (route) => {
    const body = route.request().postDataJSON() as BacklogAddRequest;
    server.adds.push(body);
    made += 1;
    if (made === 1) await hold;
    const sprint =
      current.sections.find((s) => s.sprint?.id === body.sprintId)?.sprint ??
      current.rows.find((r) => r.sprint?.id === body.sprintId)?.sprint ??
      null;
    const row: BacklogRowDto = itemRow({
      key: `SHIP-${100 + made}`,
      id: `made-${made}`,
      title: body.items[0].title,
      sprint,
      parent: null,
    });
    const put = (rows: BacklogRowDto[]) => {
      const at =
        body.placement?.slot === 'first'
          ? 0
          : rows.findIndex((r) => r.id === body.placement?.beforeItemId);
      return at < 0 ? [...rows, row] : [...rows.slice(0, at), row, ...rows.slice(at)];
    };
    current =
      body.sprintId === null && current.sections.length === 0
        ? { ...current, rows: put(current.rows) }
        : {
            ...current,
            sections: current.sections.map((s) =>
              (s.sprint?.id ?? null) === body.sprintId ? { ...s, rows: put(s.rows) } : s,
            ),
          };
    server.setBacklog(current);
    return route.fulfill(
      json({
        created: [{ id: row.id, key: row.key, revision: 1, changes: [], notices: [] }],
      }),
    );
  });
}

async function focusRow(
  page: { getByTestId(id: string): { nth(index: number): { click(): Promise<void> } } },
  index: number,
) {
  await page.getByTestId(ids.row).nth(index).click();
}

test('AddBelow_command_opens_under_the_focused_row', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<AddHereHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);

  await focusRow(page, 1);
  await page.getByTestId('run-command-addBelow').click();

  const row = page.getByTestId(ids.addRow);
  await expect(row).toHaveCount(1);
  await expect(row).toContainText('Goes below SHIP-2');
  await expect(page.getByTestId(ids.addTitle)).toBeFocused();
  const above = await row.evaluate((el) => el.previousElementSibling?.getAttribute('data-key'));
  expect(above).toBe('SHIP-2');
});

test('Three_typed_rows_keep_their_order', async ({ mount, page }) => {
  const view = backlogView(ROWS);
  const server = await stubBacklog(page, view);
  await stubCreates(page, server, view);
  await mount(<AddHereHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await focusRow(page, 0);
  await page.getByTestId('run-command-addBelow').click();

  for (const title of ['First', 'Second', 'Third']) {
    await page.getByTestId(ids.addTitle).fill(title);
    await page.getByTestId(ids.addTitle).press('Enter');
    await expect(page.getByTestId(ids.row).filter({ hasText: title })).toHaveCount(1);
    await expect(page.getByTestId(ids.addTitle)).toHaveValue('');
  }

  // Each one went in just above SHIP-2, the row that followed the anchor, and the anchor moved to the one made.
  expect(server.adds.map((add) => add.placement)).toEqual(
    Array(3).fill({ slot: 'before', beforeItemId: ROWS[1].id }),
  );
  await expect(page.getByTestId(ids.row)).toHaveText([
    'Title of SHIP-1',
    'First',
    'Second',
    'Third',
    'Title of SHIP-2',
    'Title of SHIP-3',
  ]);
  const above = await page
    .getByTestId(ids.addRow)
    .evaluate((el) => el.previousElementSibling?.getAttribute('data-key'));
  expect(above).toBe('SHIP-103');
});

test('Parent_sticks_between_rows', async ({ mount, page }) => {
  const view = backlogView(ROWS);
  const server = await stubBacklog(page, view);
  await stubCreates(page, server, view);
  await stubFeatures(page);
  await mount(<AddHereHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await focusRow(page, 0);
  await page.getByTestId('run-command-addBelow').click();
  await page.getByTestId(ids.addParent).click();
  await page.getByText('Mobile sign-in').click();

  await page.getByTestId(ids.addTitle).fill('One');
  await page.getByTestId(ids.addTitle).press('Enter');
  await expect(page.getByTestId(ids.row).filter({ hasText: 'One' })).toHaveCount(1);
  await expect(page.getByTestId(ids.addParent)).toContainText('Mobile sign-in');
  await page.getByTestId(ids.addTitle).fill('Two');
  await page.getByTestId(ids.addTitle).press('Enter');

  await expect.poll(() => server.adds.length).toBe(2);
  expect(server.adds.map((add) => add.items[0].parentId)).toEqual(['f1', 'f1']);
});

test('Points_clear_after_each_add', async ({ mount, page }) => {
  const view = backlogView(ROWS);
  const server = await stubBacklog(page, view);
  await stubCreates(page, server, view);
  await mount(<AddHereHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await focusRow(page, 0);
  await page.getByTestId('run-command-addBelow').click();

  await page.getByTestId(ids.addTitle).fill('Sized');
  await page.getByTestId(ids.addPoints).fill('5');
  await page.getByTestId(ids.addTitle).press('Enter');
  await expect(page.getByTestId(ids.row).filter({ hasText: 'Sized' })).toHaveCount(1);
  await expect(page.getByTestId(ids.addPoints)).toHaveValue('');
  await page.getByTestId(ids.addTitle).fill('Unsized');
  await page.getByTestId(ids.addTitle).press('Enter');

  await expect.poll(() => server.adds.length).toBe(2);
  expect(server.adds.map((add) => add.items[0].points)).toEqual([5, null]);
});

// Changed rule (brief: "the first item goes at the top of the sprint, each next one under the row just made"): the
// first row sends `first` with the sprint, not `last`; the second goes before the row that followed the first.
test('Add_in_a_sprint_sends_its_sprint', async ({ mount, page }) => {
  const view = planningView();
  const server = await stubBacklog(page, view);
  await stubCreates(page, server, view);
  await mount(<AddHereHarness project={PROJECT} mode="planning" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await page.getByTestId(ids.addHere).nth(1).click();
  await page.getByTestId(ids.addTitle).fill('Into sprint 25');
  await page.getByTestId(ids.addTitle).press('Enter');
  await expect(page.getByTestId(ids.row)).toHaveCount(6);
  await page.getByTestId(ids.addTitle).fill('Also sprint 25');
  await page.getByTestId(ids.addTitle).press('Enter');
  await expect(page.getByTestId(ids.row)).toHaveCount(7);

  expect(server.adds.map((add) => [add.sprintId, add.placement])).toEqual([
    ['sp25', { slot: 'first', beforeItemId: null }],
    ['sp25', { slot: 'before', beforeItemId: expect.any(String) }],
  ]);
  // They sit at the top of Sprint 25's rows, in the order typed, above SHIP-158.
  await expect(page.getByTestId(ids.row)).toHaveText([
    'Title of SHIP-160',
    'Into sprint 25',
    'Also sprint 25',
    'Title of SHIP-158',
    'Title of SHIP-170',
    'Title of SHIP-171',
    'Title of SHIP-172',
  ]);
});

test('Esc_closes_and_returns_focus_to_the_row', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<AddHereHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await focusRow(page, 1);
  await page.getByTestId('run-command-addBelow').click();
  await page.getByTestId(ids.addTitle).press('Escape');

  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);
  await expect(page.getByTestId(ids.row).nth(1)).toBeFocused();
});

test('A_failed_create_keeps_the_text', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await page.route('**/api/projects/p1/backlog/items', (route) => {
    server.adds.push(route.request().postDataJSON() as BacklogAddRequest);
    return route.fulfill(json({ code: 'validation', error: 'Title is too long.' }, 400));
  });
  await mount(
    <>
      <AddHereHarness project={PROJECT} />
      <ToastHost />
    </>,
    route,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await focusRow(page, 0);
  await page.getByTestId('run-command-addBelow').click();
  await page.getByTestId(ids.addTitle).fill('Keep me');
  await page.getByTestId(ids.addPoints).fill('3');
  await page.getByTestId(ids.addTitle).press('Enter');

  await expect(page.getByTestId(ids.addError)).toHaveText('Title is too long.');
  await expect(page.getByTestId(ids.addTitle)).toHaveValue('Keep me');
  await expect(page.getByTestId(ids.addPoints)).toHaveValue('3');
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await expect(
    page.getByRole('alert').filter({ hasText: 'Could not add "Keep me"' }),
  ).toContainText('Title is too long.');
});

test('AddBelow_is_off_when_filtering', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(
    <>
      <AddHereHarness project={PROJECT} filtering />
      <ToastHost />
    </>,
    route,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await focusRow(page, 1);
  await page.getByTestId('run-command-addBelow').click();

  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);
  await expect(page.getByRole('status').or(page.getByRole('alert'))).toContainText(
    'Adding below a row is off while a filter is on',
  );
  expect(server.adds).toHaveLength(0);
});

test('New_work_item_still_adds_at_the_top', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));

  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);

  await page.getByTestId(ids.newItem).click();
  await page.getByTestId(ids.addTitle).fill('At the top');
  await page.getByTestId(ids.addTitle).press('Enter');

  await expect.poll(() => server.creates.length).toBe(1);
  expect(server.creates[0]).toMatchObject({ type: 'story', title: 'At the top' });
  expect(server.adds).toHaveLength(0);
});

test('Rapid_Enter_while_a_write_is_in_flight_keeps_the_typed_order', async ({ mount, page }) => {
  const view = backlogView(ROWS);
  const server = await stubBacklog(page, view);
  let release!: () => void;
  const hold = new Promise<void>((resolve) => (release = resolve));
  await stubCreates(page, server, view, hold);
  await mount(<AddHereHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await focusRow(page, 0);
  await page.getByTestId('run-command-addBelow').click();

  // The first write is held open. The next two are typed and sent with Enter while it is.
  const title = page.getByTestId(ids.addTitle);
  await title.fill('One');
  await title.press('Enter');
  await expect.poll(() => server.adds.length).toBe(1);
  await expect(title).toHaveValue('');
  await title.fill('Two');
  await title.press('Enter');
  await expect(title).toHaveValue('');
  await title.fill('Three');
  await title.press('Enter');
  await expect(title).toHaveValue('');
  expect(server.adds).toHaveLength(1);

  release();
  await expect.poll(() => server.adds.length).toBe(3);
  await expect(page.getByTestId(ids.row)).toHaveText([
    'Title of SHIP-1',
    'One',
    'Two',
    'Three',
    'Title of SHIP-2',
    'Title of SHIP-3',
  ]);
  expect(server.adds.map((add) => add.items[0].title)).toEqual(['One', 'Two', 'Three']);
  // None of them went in as a second row under SHIP-1, ahead of the one before.
  expect(server.adds.map((add) => add.placement)).toEqual(
    Array(3).fill({ slot: 'before', beforeItemId: ROWS[1].id }),
  );
});

test('Rapid_Enter_in_a_sprint_while_a_write_is_in_flight_keeps_the_typed_order', async ({
  mount,
  page,
}) => {
  const view = planningView();
  const server = await stubBacklog(page, view);
  let release!: () => void;
  const hold = new Promise<void>((resolve) => (release = resolve));
  await stubCreates(page, server, view, hold);
  await mount(<AddHereHarness project={PROJECT} mode="planning" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await page.getByTestId(ids.addHere).nth(1).click();
  const title = page.getByTestId(ids.addTitle);
  await title.fill('One');
  await title.press('Enter');
  await expect.poll(() => server.adds.length).toBe(1);
  await expect(title).toHaveValue('');
  await title.fill('Two');
  await title.press('Enter');
  await expect(title).toHaveValue('');
  await title.fill('Three');
  await title.press('Enter');
  await expect(title).toHaveValue('');
  expect(server.adds).toHaveLength(1);

  release();
  await expect.poll(() => server.adds.length).toBe(3);
  await expect(page.getByTestId(ids.row)).toHaveText([
    'Title of SHIP-160',
    'One',
    'Two',
    'Three',
    'Title of SHIP-158',
    'Title of SHIP-170',
    'Title of SHIP-171',
    'Title of SHIP-172',
  ]);
  // Only the first goes first in the sprint; the others go before the row that followed it.
  expect(server.adds.map((add) => add.placement?.slot)).toEqual(['first', 'before', 'before']);
});

test('A_completion_after_the_row_reopens_does_not_move_the_new_row', async ({ mount, page }) => {
  const view = backlogView(ROWS);
  const server = await stubBacklog(page, view);
  let release!: () => void;
  const hold = new Promise<void>((resolve) => (release = resolve));
  await stubCreates(page, server, view, hold);
  await mount(<AddHereHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await focusRow(page, 0);
  await page.getByTestId('run-command-addBelow').click();
  const title = page.getByTestId(ids.addTitle);
  await title.fill('Old session');
  await title.press('Enter');
  await expect.poll(() => server.adds.length).toBe(1);

  // The row closes and opens again under SHIP-2 while the first write is still open.
  await title.press('Escape');
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);
  await focusRow(page, 1);
  await page.getByTestId('run-command-addBelow').click();
  await expect(page.getByTestId(ids.addRow)).toHaveAttribute('data-anchor', 'SHIP-2');

  release();
  // The row it made still appears, from the refetch, and the new row stays under SHIP-2.
  await expect(page.getByTestId(ids.row)).toHaveCount(4);
  await expect(page.getByTestId(ids.addRow)).toHaveAttribute('data-anchor', 'SHIP-2');
  await page.getByTestId(ids.addTitle).fill('New session');
  await page.getByTestId(ids.addTitle).press('Enter');
  await expect.poll(() => server.adds.length).toBe(2);
  expect(server.adds[1].placement).toEqual({ slot: 'before', beforeItemId: ROWS[2].id });
});

test('A_sprint_completion_after_the_row_closes_does_not_seed_the_next_one', async ({
  mount,
  page,
}) => {
  const view = planningView();
  const server = await stubBacklog(page, view);
  let release!: () => void;
  const hold = new Promise<void>((resolve) => (release = resolve));
  await stubCreates(page, server, view, hold);
  await mount(<AddHereHarness project={PROJECT} mode="planning" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await page.getByTestId(ids.addHere).nth(1).click();
  await page.getByTestId(ids.addTitle).fill('Old session');
  await page.getByTestId(ids.addTitle).press('Enter');
  await expect.poll(() => server.adds.length).toBe(1);
  await page.getByTestId(ids.addTitle).press('Escape');
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);

  release();
  await expect(page.getByTestId(ids.row)).toHaveCount(6);
  await page.getByTestId(ids.addHere).nth(1).click();
  await page.getByTestId(ids.addTitle).fill('New session');
  await page.getByTestId(ids.addTitle).press('Enter');
  await expect.poll(() => server.adds.length).toBe(2);
  expect(server.adds[1].placement).toEqual({ slot: 'first', beforeItemId: null });
});

test('A_failed_create_in_a_queue_gives_the_text_back', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await page.route('**/api/projects/p1/backlog/items', (route) => {
    server.adds.push(route.request().postDataJSON() as BacklogAddRequest);
    return route.fulfill(json({ code: 'validation', error: 'Title is too long.' }, 400));
  });
  await mount(<AddHereHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await focusRow(page, 0);
  await page.getByTestId('run-command-addBelow').click();
  await page.getByTestId(ids.addTitle).fill('Back again');
  await page.getByTestId(ids.addTitle).press('Enter');
  await expect(page.getByTestId(ids.addError)).toBeVisible();
  await expect(page.getByTestId(ids.addTitle)).toHaveValue('Back again');
});

test('A_closed_screen_does_not_leave_its_add_row_state_behind', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  const screen = await mount(<AddHereHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await focusRow(page, 0);
  await page.getByTestId('run-command-addBelow').click();
  await page.getByTestId(ids.addType).selectOption('bug');
  await page.getByTestId(ids.addTitle).fill('Left behind');
  await screen.unmount();

  await mount(<AddHereHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);
  await focusRow(page, 1);
  await page.getByTestId('run-command-addBelow').click();
  await expect(page.getByTestId(ids.addType)).toHaveValue('story');
  await expect(page.getByTestId(ids.addTitle)).toHaveValue('');
});
