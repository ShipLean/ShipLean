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

// A create that really adds the row, where the request says: before the named row, else last in its sprint.
async function stubCreates(page: Page, server: BacklogServer, view: BacklogViewDto) {
  let current = view;
  let made = 0;
  await page.route('**/api/projects/p1/backlog/items', (route) => {
    const body = route.request().postDataJSON() as BacklogAddRequest;
    server.adds.push(body);
    made += 1;
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
      const at = rows.findIndex((r) => r.id === body.placement?.beforeItemId);
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

  await expect.poll(() => server.adds.length).toBe(1);
  expect(server.adds[0].sprintId).toBe('sp25');
  expect(server.adds[0].placement).toEqual({ slot: 'last', beforeItemId: null });
  await expect(page.getByTestId(ids.row)).toHaveCount(6);
  await expect(page.getByTestId(ids.row).filter({ hasText: 'Into sprint 25' })).toHaveCount(1);
  // It sits in Sprint 25's rows, right after SHIP-158.
  const before = await page
    .getByTestId(ids.row)
    .filter({ hasText: 'Into sprint 25' })
    .evaluate((el) => el.previousElementSibling?.getAttribute('data-key'));
  expect(before).toBe('SHIP-158');
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
  await mount(<AddHereHarness project={PROJECT} />, route);
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
