import { expect, test } from '@playwright/experimental-ct-react';
import type { ChildSummaryDto } from '@shiplean/core/api/workItems';
import { priya, story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { WorkItemDetailsHarness } from '../ct/WorkItemDetailsHarness';
import { workItemTestIds as ids } from '../testIds';

// The Tasks section on the CT phone project (390×844, SHIP-714 D8, SHIP-805 rules 13/14/C5).

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const longTitle = 'Wire the vault client so card tokens are requested before the order is placed';
const task: ChildSummaryDto = {
  id: 't142',
  key: 'SHIP-142',
  type: { id: 'task', name: 'Task' },
  title: longTitle,
  state: { id: 's-t-active', name: 'In Progress', category: 'inProgress' },
  assignee: priya,
  points: null,
};

test('TaskRow_WhenTouch_ShouldStackTitleOverMeta', async ({ mount, page }) => {
  await stubApi(page, { ...story, children: [task] });
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  const row = screen.getByTestId(ids.taskRow);
  await expect(row).toHaveCount(1);

  const box = async (id: string) => (await row.getByTestId(id).boundingBox())!;
  const title = await box(ids.taskOpen);
  const key = await box(ids.taskKey);
  const state = await box(ids.taskState);
  const rowBox = (await row.boundingBox())!;
  // The long title wraps (more than one line) and the key and the state sit below it, not beside it.
  // (the link has a 32px min-height, so height alone proves nothing: check it is at least two lines and not clipped.)
  const wrap = await row.getByTestId(ids.taskOpen).evaluate((el) => ({
    lineHeight: parseFloat(getComputedStyle(el).lineHeight),
    clipped: el.scrollWidth > el.clientWidth,
  }));
  expect(wrap.clipped).toBe(false);
  expect(title.height).toBeGreaterThanOrEqual(wrap.lineHeight * 2 - 1);
  expect(key.y).toBeGreaterThanOrEqual(title.y + title.height - 1);
  expect(state.y).toBeGreaterThanOrEqual(title.y + title.height - 1);
  // The State select is a native select of at least 32px, 16px text, inside the row.
  expect(state.height).toBeGreaterThanOrEqual(32);
  expect(state.x + state.width).toBeLessThanOrEqual(rowBox.x + rowBox.width);
  const fontSize = await row
    .getByTestId(ids.taskState)
    .evaluate((select) => getComputedStyle(select).fontSize);
  expect(fontSize).toBe('16px');
  expect(await row.getByTestId(ids.taskState).evaluate((el) => el.tagName)).toBe('SELECT');
});

test('AddTaskForm_WhenTouch_ShouldUseTouchSizedControls', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
  await screen.getByTestId(ids.taskAdd).tap();

  const input = (await screen.getByTestId(ids.taskInput).boundingBox())!;
  const add = (await screen.getByTestId(ids.taskSubmit).boundingBox())!;
  const open = (await screen.getByTestId(ids.taskAdd).boundingBox())!;
  expect(input.height).toBeGreaterThanOrEqual(32);
  expect(add.height).toBeGreaterThanOrEqual(32);
  expect(open.height).toBeGreaterThanOrEqual(32);
  expect(
    await screen.getByTestId(ids.taskInput).evaluate((el) => getComputedStyle(el).fontSize),
  ).toBe('16px');
});
