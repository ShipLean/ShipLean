import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import { backlogView, itemRow, planningView, PROJECT, ROWS } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { PlanningMode } from '../planning/PlanningMode';
import { backlogTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };

const THREE = 'Checkout\n  Confirm dialog\nbug: Session expires (2 pts)';

async function openAddRow(page: Page) {
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  await page.getByTestId(ids.newItem).click();
  await expect(page.getByTestId(ids.addTitle)).toBeFocused();
}

async function paste(page: Page, text: string) {
  await page.getByTestId(ids.addTitle).evaluate((input, value) => {
    const data = new DataTransfer();
    data.setData('text', value);
    input.dispatchEvent(
      new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }),
    );
  }, text);
}

const titles = (page: Page) => page.getByTestId(ids.pasteRowTitle);

test('Two_lines_open_the_review', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openAddRow(page);
  await page.getByTestId(ids.addType).selectOption('bug');

  await paste(page, THREE);

  await expect(page.getByTestId(ids.pasteDialog)).toBeVisible();
  await expect(page.getByTestId(ids.pasteRow)).toHaveCount(3);
  await expect(titles(page).nth(0)).toHaveValue('Checkout');
  await expect(page.getByTestId(ids.pasteRow).nth(1)).toHaveAttribute('data-kind', 'task');
  await expect(page.getByTestId(ids.pasteRowType).nth(0)).toHaveValue('bug');
  await expect(page.getByTestId(ids.pasteRowPoints).nth(2)).toHaveValue('2');
  await expect(page.getByTestId(ids.pasteCreate)).toHaveText('Add 2 items and 1 task');
});

test('One_line_stays_in_the_field', async ({ mount, page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openAddRow(page);
  await page.evaluate(() => navigator.clipboard.writeText('Just one line\n'));

  // A real paste: the browser inserts the text only if the intake leaves a one-line paste alone.
  await page.keyboard.press('ControlOrMeta+V');

  await expect(page.getByTestId(ids.addTitle)).toHaveValue('Just one line');
  await expect(page.getByTestId(ids.pasteDialog)).toHaveCount(0);
});

test('Edit_a_row_before_creating', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openAddRow(page);
  await paste(page, 'One\nTwo');

  await titles(page).nth(1).fill('Second');
  await page.getByTestId(ids.pasteRowPoints).nth(1).fill('x');
  await expect(page.getByTestId(ids.pasteCreate)).toBeDisabled();
  await page.getByTestId(ids.pasteRowPoints).nth(1).fill('5');
  await page.getByTestId(ids.pasteRowType).nth(0).selectOption('bug');
  expect(server.adds).toHaveLength(0);

  const created = page.waitForResponse((r) => r.url().endsWith('/backlog/items'));
  await page.getByTestId(ids.pasteCreate).click();
  await created;

  expect(server.adds).toHaveLength(1);
  expect(server.adds[0].items.map((i) => [i.type, i.title, i.points])).toEqual([
    ['bug', 'One', null],
    ['story', 'Second', 5],
  ]);
  expect(server.adds[0].sprintId).toBeNull();
  expect(server.adds[0].placement).toEqual({ slot: 'first', beforeItemId: null });
  await expect(page.getByTestId(ids.pasteDialog)).toHaveCount(0);
});

test('Likely_duplicate_is_flagged', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  server.duplicatesFor([{ index: 1, items: [{ key: 'SHIP-12', title: 'Two, already' }] }]);
  await mount(<ListMode project={PROJECT} />, route);
  await openAddRow(page);
  await paste(page, 'One\nTwo\n  A task');

  await expect(page.getByTestId(ids.pasteRowDuplicate)).toHaveCount(1);
  await expect(page.getByTestId(ids.pasteRowDuplicate)).toHaveText(
    'Looks like SHIP-12: Two, already',
  );
  expect(server.duplicateChecks).toEqual([{ titles: ['One', 'Two'] }]);

  // Editing a title asks again, about that title only.
  await titles(page).nth(0).fill('One edited');
  await titles(page).nth(0).blur();
  await expect.poll(() => server.duplicateChecks.length).toBe(2);
  expect(server.duplicateChecks[1]).toEqual({ titles: ['One edited'] });
});

// Pasting into the dialog's own box checks the new titles at once; typing there still waits for the box to lose focus.
test('Pasting_into_the_review_box_flags_a_duplicate_before_the_box_loses_focus', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openAddRow(page);
  await paste(page, 'One\nTwo');
  await expect(page.getByTestId(ids.pasteRow)).toHaveCount(2);
  await expect.poll(() => server.duplicateChecks.length).toBe(1);
  server.duplicatesFor([{ index: 0, items: [{ key: 'SHIP-12', title: 'Three, already' }] }]);

  const box = page.getByTestId(ids.pasteSource);
  await box.click();
  await box.press('Control+End');
  await box.dispatchEvent('paste');
  await page.keyboard.insertText('\nThree');

  await expect(box).toBeFocused();
  await expect(page.getByTestId(ids.pasteRowDuplicate)).toHaveText(
    'Looks like SHIP-12: Three, already',
  );
  expect(server.duplicateChecks).toEqual([{ titles: ['One', 'Two'] }, { titles: ['Three'] }]);

  // Typing is not a paste: no check until the box loses focus.
  await page.keyboard.insertText(' more');
  await expect(titles(page).nth(2)).toHaveValue('Three more');
  expect(server.duplicateChecks).toHaveLength(2);
  await box.blur();
  await expect.poll(() => server.duplicateChecks.length).toBe(3);
  expect(server.duplicateChecks[2]).toEqual({ titles: ['Three more'] });
});

test('Keep_as_one_item_sends_one_item_with_the_paste_as_its_description', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openAddRow(page);
  await paste(page, THREE);

  const created = page.waitForResponse((r) => r.url().endsWith('/backlog/items'));
  await page.getByTestId(ids.pasteKeepOne).click();
  await created;

  expect(server.adds[0].items).toEqual([
    {
      type: 'story',
      title: 'Checkout',
      description: THREE,
      points: null,
      parentId: null,
      tasks: [],
    },
  ]);
});

test('Create_sends_tasks_under_their_story', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openAddRow(page);
  await paste(page, 'Checkout\t3\n\tConfirm\t1\n\tReceipt\nApple Pay');

  const created = page.waitForResponse((r) => r.url().endsWith('/backlog/items'));
  await page.getByTestId(ids.pasteCreate).click();
  await created;

  expect(server.adds[0].items).toEqual([
    {
      type: 'story',
      title: 'Checkout',
      description: null,
      points: 3,
      parentId: null,
      tasks: [
        { title: 'Confirm', points: 1 },
        { title: 'Receipt', points: null },
      ],
    },
    {
      type: 'story',
      title: 'Apple Pay',
      description: null,
      points: null,
      parentId: null,
      tasks: [],
    },
  ]);
});

test('Over_100_turns_create_off', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openAddRow(page);
  const lines = (count: number) => Array.from({ length: count }, (_, i) => `Line ${i}`).join('\n');

  await paste(page, lines(101));
  await expect(page.getByTestId(ids.bulkLimit)).toBeVisible();
  await expect(page.getByTestId(ids.pasteCreate)).toBeDisabled();

  await page.getByTestId(ids.pasteSource).fill(lines(100));
  await expect(page.getByTestId(ids.bulkLimit)).toHaveCount(0);
  await expect(page.getByTestId(ids.pasteCreate)).toBeEnabled();
});

test('A_failed_create_keeps_the_grid', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await page.route('**/api/projects/p1/backlog/items', (route) =>
    route.fulfill({
      status: 400,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Title is too long.', code: 'validation' }),
    }),
  );
  await mount(
    <>
      <ListMode project={PROJECT} />
      <ToastHost />
    </>,
    route,
  );
  await openAddRow(page);
  await paste(page, 'One\nTwo');
  await titles(page).nth(0).fill('One edited');

  await page.getByTestId(ids.pasteCreate).click();

  await expect(page.getByTestId(ids.addError)).toHaveText('Title is too long.');
  await expect(page.getByTestId(componentTestIds.toast)).toHaveAttribute('data-tone', 'danger');
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Title is too long.');
  await expect(page.getByTestId(ids.pasteDialog)).toBeVisible();
  await expect(page.getByTestId(ids.pasteRow)).toHaveCount(2);
  await expect(titles(page).nth(0)).toHaveValue('One edited');
  await expect(page.getByTestId(ids.pasteCreate)).toBeEnabled();
});

// "Put them": the request each choice sends (K2). Rows a, b, c share the Backlog; d and e are in Sprint 25.
const PLACE_ROWS = [
  itemRow({ key: 'SHIP-1', rank: 1 }),
  itemRow({ key: 'SHIP-2', rank: 2 }),
  itemRow({ key: 'SHIP-3', rank: 3 }),
  itemRow({ key: 'SHIP-4', rank: 4, sprint: { id: 'sp25', name: 'Sprint 25', status: 'future' } }),
  itemRow({ key: 'SHIP-5', rank: 5, sprint: { id: 'sp25', name: 'Sprint 25', status: 'future' } }),
  itemRow({ key: 'SHIP-6', rank: 6, sprint: { id: 'sp24', name: 'Sprint 24', status: 'current' } }),
];

async function openPaste(page: Page, focusKey: string | null) {
  await expect(page.getByTestId(ids.row)).toHaveCount(PLACE_ROWS.length);
  if (focusKey !== null)
    await page
      .getByTestId(ids.row)
      .filter({ hasText: focusKey })
      .getByTestId(ids.rowSelect)
      .click();
  await page.getByTestId(ids.newItem).click();
  await paste(page, 'One\nTwo');
}

async function createWith(page: Page, choice: string | null) {
  if (choice !== null) await page.getByTestId(ids.pastePlace).selectOption({ label: choice });
  const created = page.waitForResponse((r) => r.url().endsWith('/backlog/items'));
  await page.getByTestId(ids.pasteCreate).click();
  await created;
}

const optionLabels = (page: Page) => page.getByTestId(ids.pastePlace).locator('option');

test('Put_them_offers_top_bottom_and_future_sprints_without_a_focused_row', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, backlogView(PLACE_ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openPaste(page, null);

  await expect(optionLabels(page)).toHaveText([
    'Top of the backlog',
    'Bottom of the backlog',
    'Sprint 25',
  ]);
  await expect(page.getByTestId(ids.pastePlace)).toHaveValue('top');
});

test('Put_them_top_sends_first', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(PLACE_ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openPaste(page, null);
  await createWith(page, 'Top of the backlog');
  expect(server.adds[0].sprintId).toBeNull();
  expect(server.adds[0].placement).toEqual({ slot: 'first', beforeItemId: null });
});

test('Put_them_bottom_sends_last', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(PLACE_ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openPaste(page, null);
  await createWith(page, 'Bottom of the backlog');
  expect(server.adds[0].sprintId).toBeNull();
  expect(server.adds[0].placement).toEqual({ slot: 'last', beforeItemId: null });
});

test('Put_them_in_a_sprint_sends_its_id', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(PLACE_ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openPaste(page, null);
  await createWith(page, 'Sprint 25');
  expect(server.adds[0].sprintId).toBe('sp25');
  expect(server.adds[0].placement).toEqual({ slot: 'first', beforeItemId: null });
});

test('Put_them_below_the_focused_row_is_the_default_and_goes_before_the_next_row', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, backlogView(PLACE_ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openPaste(page, 'SHIP-1');

  await expect(optionLabels(page)).toContainText(['Below SHIP-1']);
  await expect(page.getByTestId(ids.pastePlace)).toHaveValue('below');
  await createWith(page, null);

  expect(server.adds[0].sprintId).toBeNull();
  expect(server.adds[0].placement).toEqual({ slot: 'before', beforeItemId: 'id-SHIP-2' });
});

test('Put_them_below_the_last_row_of_a_sprint_goes_before_the_next_listed_row_in_its_sprint', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, backlogView(PLACE_ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openPaste(page, 'SHIP-5');
  await createWith(page, null);
  expect(server.adds[0].sprintId).toBe('sp25');
  expect(server.adds[0].placement).toEqual({ slot: 'before', beforeItemId: 'id-SHIP-6' });
});

test('Put_them_below_the_last_listed_row_lands_last', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(PLACE_ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openPaste(page, 'SHIP-6');
  await createWith(page, null);
  expect(server.adds[0].sprintId).toBe('sp24');
  expect(server.adds[0].placement).toEqual({ slot: 'last', beforeItemId: null });
});

test('Put_them_below_a_row_inside_a_sprint_lands_in_that_sprint', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(PLACE_ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openPaste(page, 'SHIP-4');
  await createWith(page, null);
  expect(server.adds[0].sprintId).toBe('sp25');
  expect(server.adds[0].placement).toEqual({ slot: 'before', beforeItemId: 'id-SHIP-5' });
});

test('Put_them_makes_no_extra_request', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(PLACE_ROWS));
  const requests: string[] = [];
  page.on('request', (r) => requests.push(new URL(r.url()).pathname));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(PLACE_ROWS.length);
  const before = requests.filter((p) => p.endsWith('/backlog')).length;
  await openPaste(page, null);
  await expect(page.getByTestId(ids.pastePlace)).toBeVisible();
  expect(requests.filter((p) => p.endsWith('/backlog')).length).toBe(before);
  expect(server.adds).toHaveLength(0);
});

// Planning: each section is the one backlog stack filtered by sprint, so the next row is the next row of the section.
const PLANNING_ROWS = 5;
const planningRoute = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };

async function openPlanningPaste(page: Page, focusKey: string) {
  await expect(page.getByTestId(ids.row)).toHaveCount(PLANNING_ROWS);
  await page.getByTestId(ids.row).filter({ hasText: focusKey }).getByTestId(ids.rowSelect).click();
  await page.getByTestId(ids.newItem).click();
  await paste(page, 'One\nTwo');
}

test('Planning_PutThemBelowARowInASection_GoesBeforeTheNextRowOfThatSection', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, planningRoute);
  await openPlanningPaste(page, 'SHIP-170');
  await createWith(page, null);
  expect(server.adds[0].sprintId).toBeNull();
  expect(server.adds[0].placement).toEqual({ slot: 'before', beforeItemId: 'id-SHIP-171' });
});

test('Planning_PutThemBelowTheLastRowOfASection_LandsLastInThatSection', async ({
  mount,
  page,
}) => {
  const view = planningView();
  const server = await stubBacklog(page, view);
  await mount(<PlanningMode project={PROJECT} />, planningRoute);
  await openPlanningPaste(page, 'SHIP-158');
  await createWith(page, null);
  expect(server.adds[0].sprintId).toBe(view.sections[1].sprint?.id);
  expect(server.adds[0].placement).toEqual({ slot: 'last', beforeItemId: null });
});
