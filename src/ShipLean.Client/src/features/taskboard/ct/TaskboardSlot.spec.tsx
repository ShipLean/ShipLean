import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import type { TaskboardViewDto } from '../../../api/views';
import { TaskboardSlot } from '../../../slots/TaskboardSlot';
import { taskboardTestIds as ids } from '../testIds';
import { PROJECT, SPRINT, SPRINTS, VIEW } from './fixtures';
import { stubTaskboard } from './stubApi';

const route = { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } };
const slot = <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />;

test('Taskboard_WhenLoaded_ShouldShowStoryRowsWithUnparentedLast', async ({ mount, page }) => {
  await stubTaskboard(page, VIEW);
  await mount(slot, route);

  const rows = page.getByTestId(ids.row);
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(0)).toHaveAttribute('data-row', 'id-SHIP-141');
  await expect(rows.nth(2)).toHaveAttribute('data-row', 'unparented');
  await expect(rows.nth(0).getByTestId(ids.rowProgress)).toHaveText('☑ 1/3');
  await expect(rows.nth(0).getByTestId(ids.card)).toHaveCount(3);
  await expect(page.getByTestId(ids.columnHeader)).toHaveCount(3);
});

test('Taskboard_WhenStoryCompleted_ShouldStartCollapsedAndExpandOnToggle', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, VIEW);
  await mount(slot, route);

  const done = page.getByTestId(ids.row).nth(1);
  await expect(done).toHaveAttribute('data-collapsed', 'true');
  await expect(done.getByTestId(ids.card)).toHaveCount(0);
  await expect(done.getByTestId(ids.cellCount)).toHaveText(['', '', '1']);
  await expect(page.getByTestId(ids.row).nth(0)).toHaveAttribute('data-collapsed', 'false');

  await done.getByTestId(ids.rowToggle).click();
  await expect(done).toHaveAttribute('data-collapsed', 'false');
  await expect(done.getByTestId(ids.card)).toHaveCount(1);

  await page.getByTestId(ids.row).nth(0).getByTestId(ids.rowToggle).click();
  await expect(page.getByTestId(ids.row).nth(0).getByTestId(ids.card)).toHaveCount(0);
});

test('Taskboard_WhenGroupedByPeople_ShouldShowCountsOnlyAndSavePreference', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW);
  await mount(slot, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);

  await page.getByTestId(ids.groupOption).filter({ hasText: 'People' }).click();

  const rows = page.getByTestId(ids.row);
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(0)).toHaveAttribute('data-row', 'unassigned');
  await expect(rows.nth(0).getByTestId(ids.rowCount)).toHaveText('2 tasks');
  await expect(rows.nth(1).getByTestId(ids.rowCount)).toHaveText('1 task');
  await expect(page.getByTestId(ids.rowState)).toHaveCount(0);
  await expect(page.getByTestId(ids.addTask)).toHaveCount(0);
  await expect(page.getByTestId(ids.rowProgress)).toHaveCount(0);
  await expect(rows.nth(0).getByTestId(ids.cardParent).first()).toContainText('Story SHIP-141');
  expect(server.puts).toEqual([{ groupBy: 'people' }]);
});

test('Taskboard_WhenPreferenceIsPeople_ShouldOpenGroupedByPeople', async ({ mount, page }) => {
  await stubTaskboard(page, VIEW, { saved: { groupBy: 'people' } });
  await mount(slot, route);
  await expect(page.getByTestId(ids.row).first()).toHaveAttribute('data-row', 'unassigned');
});

// The faces are the people with tasks (Priya, Tom), then Unassigned.
const face = (page: Page, who: 'priya' | 'tom' | 'unassigned') =>
  page.getByTestId(ids.face).nth({ priya: 0, tom: 1, unassigned: 2 }[who]);
const counts = (page: Page) => page.getByTestId(ids.columnCount);

test('Taskboard_WhenFaceChosen_ShouldShowOnlyThatPersonsCardsAndRecountColumns', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, VIEW);
  await mount(slot, route);
  await expect(page.getByTestId(ids.card)).toHaveCount(4);
  await expect(counts(page)).toHaveText(['2', '1', '2']);

  await face(page, 'tom').click();
  await expect(page.getByTestId(ids.card)).toHaveCount(1);
  await expect(page.getByTestId(ids.cardKey)).toHaveText('SHIP-172');
  await expect(counts(page)).toHaveText(['0', '1', '1']);

  await face(page, 'unassigned').click();
  await expect(page.getByTestId(ids.card)).toHaveCount(3);
  await expect(counts(page)).toHaveText(['2', '1', '1']);

  await face(page, 'tom').click();
  await face(page, 'unassigned').click();
  await expect(page.getByTestId(ids.card)).toHaveCount(4);
  await expect(counts(page)).toHaveText(['2', '1', '2']);
});

test('Taskboard_WhenFaceChosenInPeopleRows_ShouldHideOtherPeopleAndRecountColumns', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, VIEW, { saved: { groupBy: 'people' } });
  await mount(slot, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await expect(counts(page)).toHaveText(['2', '1', '2']);

  await face(page, 'tom').click();

  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  await expect(page.getByTestId(ids.row)).toHaveAttribute('data-row', 'u-tom');
  await expect(page.getByTestId(ids.card)).toHaveCount(2);
  await expect(counts(page)).toHaveText(['0', '1', '1']);

  await face(page, 'unassigned').click();
  await expect(page.getByTestId(ids.row)).toHaveCount(2);
  await expect(counts(page)).toHaveText(['2', '1', '1']);
});

test('Taskboard_WhenTaskAdded_ShouldKeepBoxOpenForTheNext', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW);
  await mount(slot, route);

  await page.getByTestId(ids.row).nth(0).getByTestId(ids.addTask).click();
  const title = page.getByTestId(ids.taskTitle);
  await title.fill('First');
  await title.press('Enter');
  await expect(title).toHaveValue('');
  await expect(title).toBeVisible();
  await title.fill('Second');
  await title.press('Enter');
  await expect(title).toHaveValue('');

  expect(server.creates.map((c) => [c.type, c.title, c.parentId, c.sprintId])).toEqual([
    ['task', 'First', 'id-SHIP-141', null],
    ['task', 'Second', 'id-SHIP-141', null],
  ]);
  await title.press('Escape');
  await expect(title).toHaveCount(0);
});

test('Taskboard_WhenTaskAddedInUnparentedRow_ShouldPutItInTheSprint', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW);
  await mount(slot, route);

  await page.getByTestId(ids.row).nth(2).getByTestId(ids.addTask).click();
  await page.getByTestId(ids.taskTitle).fill('Loose');
  await page.getByTestId(ids.taskTitle).press('Enter');
  await expect(page.getByTestId(ids.taskTitle)).toHaveValue('');
  expect(server.creates).toHaveLength(1);
  expect(server.creates[0]).toMatchObject({ parentId: null, sprintId: 's24', type: 'task' });
});

test('Taskboard_WhenStoryOrBugAdded_ShouldCreateIntoTheSprintAndStayOpen', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW);
  await mount(slot, route);

  await page.getByTestId(ids.addStory).click();
  await page.getByTestId(ids.addStoryType).selectOption('bug');
  await page.getByTestId(ids.addStoryTitle).fill('Crash on login');
  await page.getByTestId(ids.addStoryTitle).press('Enter');

  await expect(page.getByTestId(ids.addStoryTitle)).toHaveValue('');
  await expect(page.getByTestId(ids.addStoryBox)).toBeVisible();
  expect(server.creates).toHaveLength(1);
  expect(server.creates[0]).toMatchObject({
    type: 'bug',
    title: 'Crash on login',
    parentId: null,
    sprintId: 's24',
  });
  await page.getByTestId(ids.addStoryClose).click();
  await expect(page.getByTestId(ids.addStoryBox)).toHaveCount(0);
});

test('Taskboard_WhenParentStatePicked_ShouldPatchTheStoryAtItsRevision', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW);
  await mount(slot, route);

  await page.getByTestId(ids.row).nth(0).getByTestId(ids.rowState).selectOption('ps-closed');
  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0]).toMatchObject({
    expectedRevision: 1,
    changes: { state: { value: 'ps-closed' } },
  });
});

const withToasts = (
  <>
    <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />
    <ToastHost />
  </>
);

test('Taskboard_WhenParentStateRefused_ShouldSayWhy', async ({ mount, page }) => {
  await stubTaskboard(page, VIEW, {
    patchAnswer: {
      status: 409,
      body: { error: 'Changed by someone else.', code: 'conflict', current: {}, theirChanges: [] },
    },
  });
  await mount(withToasts, route);

  await page.getByTestId(ids.row).nth(0).getByTestId(ids.rowState).selectOption('ps-closed');

  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'SHIP-141 was changed by someone else',
  );
});

test('Taskboard_WhenFieldsSettingsHideStoryFields_ShouldHideThemInTheRowHeader', async ({
  mount,
  page,
}) => {
  const setting = {
    type: 'story' as const,
    showKey: true,
    showAssignee: true,
    showTags: true,
    showPoints: false,
    showEmptyFields: false,
    extraFields: [],
  };
  const view: TaskboardViewDto = {
    ...VIEW,
    cardFields: [setting],
    annotations: { ...VIEW.annotations, tasks: false },
  };
  await stubTaskboard(page, view);
  await mount(slot, route);

  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await expect(page.getByTestId(ids.rowPoints)).toHaveCount(0);
  await expect(page.getByTestId(ids.rowProgress)).toHaveCount(0);
});

test('Taskboard_WhenShowEmptyFieldsOn_ShouldShowADashForEmptyPoints', async ({ mount, page }) => {
  const view: TaskboardViewDto = {
    ...VIEW,
    cardFields: [
      {
        type: 'task',
        showKey: true,
        showAssignee: true,
        showTags: true,
        showPoints: true,
        showEmptyFields: true,
        extraFields: [],
      },
    ],
  };
  await stubTaskboard(page, view);
  await mount(slot, route);

  await expect(page.getByTestId(ids.cardPoints)).toHaveCount(4);
  await expect(page.getByTestId(ids.cardPoints).first()).toHaveText('–');
});

test('Taskboard_WhenStoryHasNoTasks_ShouldSayNoTasksYet', async ({ mount, page }) => {
  const view: TaskboardViewDto = {
    ...VIEW,
    rows: VIEW.rows.map((row, index) =>
      index === 0 ? { ...row, parent: { ...row.parent!, rollups: [] }, cells: [] } : row,
    ),
  };
  await stubTaskboard(page, view);
  await mount(slot, route);

  await expect(page.getByTestId(ids.row).nth(0).getByTestId(ids.rowNoTasks)).toHaveText(
    'No tasks yet',
  );
  await expect(page.getByTestId(ids.row).nth(1).getByTestId(ids.rowNoTasks)).toHaveCount(0);
});

test('Taskboard_WhenEmptyTaskBoxLosesFocus_ShouldClose', async ({ mount, page }) => {
  await stubTaskboard(page, VIEW);
  await mount(slot, route);

  await page.getByTestId(ids.row).nth(0).getByTestId(ids.addTask).click();
  await expect(page.getByTestId(ids.taskTitle)).toBeFocused();
  await page.getByTestId(ids.addStory).focus();
  await expect(page.getByTestId(ids.taskBox)).toHaveCount(0);

  await page.getByTestId(ids.row).nth(0).getByTestId(ids.addTask).click();
  await page.getByTestId(ids.taskTitle).fill('Half typed');
  await page.getByTestId(ids.addStory).focus();
  await expect(page.getByTestId(ids.taskBox)).toHaveCount(1);
});

test('Taskboard_WhenPreferenceNotYetRead_ShouldDrawNoRowsUntilItIs', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, {
    saved: { groupBy: 'people' },
    gatePreference: true,
  });
  await mount(slot, route);

  await expect.poll(() => server.reads).toBe(1);
  await expect(page.getByTestId(ids.loading)).toBeVisible();
  await expect(page.getByTestId(ids.row)).toHaveCount(0);

  server.releasePreference();

  await expect(page.getByTestId(ids.row).first()).toHaveAttribute('data-row', 'unassigned');
});

test('Taskboard_WhenGroupingChanged_ShouldKeepTheOtherSavedKeys', async ({ mount, page }) => {
  const server = await stubTaskboard(page, VIEW, { saved: { groupBy: 'stories', other: 7 } });
  await mount(slot, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);

  await page.getByTestId(ids.groupOption).filter({ hasText: 'People' }).click();

  await expect.poll(() => server.puts.length).toBe(1);
  expect(server.puts[0]).toEqual({ groupBy: 'people', other: 7 });
});
