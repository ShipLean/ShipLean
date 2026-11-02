import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { backlogView, PROJECT, ROWS } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
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

test('One_line_stays_in_the_field', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await openAddRow(page);

  await page.getByTestId(ids.addTitle).evaluate((input) => {
    const data = new DataTransfer();
    data.setData('text', 'Just one line\n');
    const event = new ClipboardEvent('paste', {
      clipboardData: data,
      bubbles: true,
      cancelable: true,
    });
    input.dispatchEvent(event);
    input.setAttribute('data-prevented', String(event.defaultPrevented));
  });

  await expect(page.getByTestId(ids.pasteDialog)).toHaveCount(0);
  await expect(page.getByTestId(ids.addTitle)).toHaveAttribute('data-prevented', 'false');
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
  expect(server.adds[0].placement).toBeNull();
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
  await mount(<ListMode project={PROJECT} />, route);
  await openAddRow(page);
  await paste(page, 'One\nTwo');
  await titles(page).nth(0).fill('One edited');

  await page.getByTestId(ids.pasteCreate).click();

  await expect(page.getByTestId(ids.addError)).toHaveText('Title is too long.');
  await expect(page.getByTestId(ids.pasteDialog)).toBeVisible();
  await expect(page.getByTestId(ids.pasteRow)).toHaveCount(2);
  await expect(titles(page).nth(0)).toHaveValue('One edited');
  await expect(page.getByTestId(ids.pasteCreate)).toBeEnabled();
});
