import { expect, test } from '@playwright/experimental-ct-react';
import type { ChildSummaryDto } from '../../../api/workItems';
import { priya, story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { stubTaskCreate } from '../ct/stubDetails';
import { WorkItemDetailsHarness } from '../ct/WorkItemDetailsHarness';
import { workItemHarnessIds as harness } from '../ct/harnessIds';
import { workItemTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const task = (
  number: number,
  title: string,
  state: ChildSummaryDto['state'],
  assignee = priya,
): ChildSummaryDto => ({
  id: `t${number}`,
  key: `SHIP-${number}`,
  type: { id: 'task', name: 'Task' },
  title,
  state,
  assignee,
  points: null,
});
const open = { id: 's-t-active', name: 'In Progress', category: 'inProgress' } as const;
const closed = { id: 's-t-done', name: 'Done', category: 'completed' } as const;
const withTasks = {
  ...story,
  children: [task(142, 'Wire the vault client', closed), task(143, 'Show decline reasons', open)],
};

test('TasksSection_WhenItemHasTasks_ShouldListThemWithStateAndDoneCount', async ({
  mount,
  page,
}) => {
  await stubApi(page, withTasks);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  const rows = screen.getByTestId(ids.taskRow);
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toContainText('Wire the vault client');
  await expect(rows.nth(0).getByTestId(ids.taskKey)).toHaveText('SHIP-142');
  await expect(rows.nth(0).getByTestId(ids.taskState)).toHaveValue('s-t-done');
  await expect(rows.nth(1).getByTestId(ids.taskState)).toHaveValue('s-t-active');
  await expect(screen.getByTestId(ids.tasksCount)).toHaveText('1/2');
});

test('TasksSection_WhenNoTasks_ShouldSayBreakItDown', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await expect(screen.getByTestId(ids.tasksEmpty)).toContainText('No tasks yet');
  await expect(screen.getByTestId(ids.tasksCount)).toHaveText('0/0');
});

test('TaskRow_WhenTitleClicked_ShouldOpenTaskModal', async ({ mount, page }) => {
  await stubApi(page, withTasks);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.taskOpen).nth(1).click();

  await expect(screen.getByTestId(harness.location)).toHaveText('/p/SHIP/items/SHIP-141');
  await expect(screen.getByTestId(harness.search)).toHaveText('?item=SHIP-143');
});

test('TasksSection_WhenTaskAdded_ShouldCreateItUnderTheItemWithoutDirtyingTheForm', async ({
  mount,
  page,
}) => {
  const api = await stubApi(page, story);
  const tasks = await stubTaskCreate(page, story, api.setCurrent);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.taskAdd).click();
  await screen.getByTestId(ids.taskInput).fill('Write the migration');
  await screen.getByTestId(ids.taskSubmit).click();

  await expect(screen.getByTestId(ids.taskRow)).toHaveCount(1);
  await expect(screen.getByTestId(ids.taskRow)).toContainText('Write the migration');
  await expect(screen.getByTestId(ids.tasksCount)).toHaveText('0/1');
  await expect(screen.getByTestId(ids.taskInput)).toHaveValue('');
  expect(tasks.created).toEqual([
    {
      projectId: 'p1',
      type: 'task',
      title: 'Write the migration',
      parentId: 'w141',
      sprintId: null,
      changes: null,
    },
  ]);
  await expect(screen.getByTestId(ids.save)).toBeDisabled();
  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 7');
  expect(api.patches).toEqual([]);
});

test('TasksSection_WhenTitleIsBlank_ShouldNotAllowAdd', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.taskAdd).click();
  await screen.getByTestId(ids.taskInput).fill('   ');

  await expect(screen.getByTestId(ids.taskSubmit)).toBeDisabled();
});

test('TasksSection_WhenEscapePressed_ShouldCloseTheForm', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.taskAdd).click();
  await screen.getByTestId(ids.taskInput).press('Escape');

  await expect(screen.getByTestId(ids.taskForm)).toHaveCount(0);
});

test('TasksSection_WhenCreateFails_ShouldShowTheErrorAndKeepTheTitle', async ({ mount, page }) => {
  const api = await stubApi(page, story);
  const tasks = await stubTaskCreate(page, story, api.setCurrent);
  tasks.failNext('A task needs a title.');
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.taskAdd).click();
  await screen.getByTestId(ids.taskInput).fill('Write the migration');
  await screen.getByTestId(ids.taskSubmit).click();

  await expect(screen.getByTestId(ids.taskError)).toHaveText('A task needs a title.');
  await expect(screen.getByTestId(ids.taskInput)).toHaveValue('Write the migration');
  await expect(screen.getByTestId(ids.taskRow)).toHaveCount(0);
});

const taskItem = { ...story, id: 't143', key: 'SHIP-143', revision: 3 };

test('TasksSection_WhenTaskStateChanged_ShouldPatchTheTaskAtItsLatestRevision', async ({
  mount,
  page,
}) => {
  const api = await stubApi(page, withTasks, undefined, { others: [taskItem] });
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.taskState).nth(1).selectOption('s-t-done');

  await expect.poll(() => api.patches.length).toBe(1);
  expect(api.patches[0]?.expectedRevision).toBe(3);
  expect(api.patches[0]?.changes.state).toEqual({ value: 's-t-done' });
  await expect(screen.getByTestId(ids.save)).toBeDisabled();
});

test('TasksSection_WhenTaskStateChangeFails_ShouldShowTheError', async ({ mount, page }) => {
  await stubApi(
    page,
    withTasks,
    () => ({ status: 400, body: { error: 'That state is not allowed.', code: 'validation' } }),
    { others: [taskItem] },
  );
  const screen = await mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.taskState).nth(1).selectOption('s-t-done');

  await expect(screen.getByTestId(ids.taskStateError)).toHaveText('That state is not allowed.');
});
