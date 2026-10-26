import type { Locator, Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../api/projects';
import type { SprintCapacityDto, SprintDto, SprintMetricsDto } from '../../../api/sprints';
import type { StateCategory, UserRef } from '../../../api/refs';
import type { ChildSummaryDto, WorkItemDto } from '../../../api/workItems';
import { componentTestIds } from '../../../components/testIds';
import { ToastHost } from '../../../components/ToastHost';
import { sprintsTestIds as ids } from '../testIds';
import { AddressReadout } from './AddressReadout';
import { SprintBacklog } from './SprintBacklog';

const project: ProjectDto = {
  id: 'p1',
  key: 'SHIP',
  name: 'Storefront',
  description: '',
  role: 'member',
  memberCount: 3,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 1,
};

const sprint: SprintDto = {
  id: 's24',
  name: 'Sprint 24',
  startDate: '2026-10-19',
  endDate: '2026-10-30',
  goal: '',
  status: 'current',
  rolloverStatus: null,
  datesLabel: '19 Oct – 30 Oct',
  timeLabel: '5 working days left',
};

const priya: UserRef = {
  id: 'u1',
  name: 'Priya',
  initials: 'P',
  avatarColour: '#3366cc',
  photoUrl: null,
};

const state = (name: string, category: StateCategory, type = 'story') => ({
  id: `${type}-${name}`,
  name,
  category,
});

const taskItem = (
  key: string,
  title: string,
  category: StateCategory,
  options: { parent?: string | null; sprint?: string | null; assignee?: UserRef | null } = {},
) => {
  const parent = options.parent === undefined ? null : options.parent;
  return {
    id: key,
    key,
    type: { id: 'task', name: 'Task' },
    title,
    state: state(
      category === 'completed' ? 'Closed' : category === 'proposed' ? 'New' : 'Active',
      category,
    ),
    assignee:
      options.assignee === undefined ? (category === 'proposed' ? null : priya) : options.assignee,
    sprint:
      options.sprint === undefined || options.sprint === null
        ? null
        : { id: options.sprint, name: 'Sprint', status: 'current' },
    parent: parent === null ? null : { id: parent, key: parent, title: 'Parent' },
    points: 2,
    revision: 4,
    children: [],
  } as unknown as WorkItemDto;
};

const childOf = (task: WorkItemDto): ChildSummaryDto => ({
  id: task.id,
  key: task.key,
  type: task.type,
  title: task.title,
  state: task.state,
  assignee: task.assignee,
  points: task.points,
});

const story = (
  key: string,
  title: string,
  points: number,
  sprintId: string | null,
  children: WorkItemDto[] = [],
  typeId: 'story' | 'bug' = 'story',
  category: StateCategory = 'inProgress',
) =>
  ({
    id: key,
    key,
    type: { id: typeId, name: typeId },
    title,
    state: state(category === 'proposed' ? 'New' : 'Active', category),
    assignee: category === 'proposed' ? null : priya,
    sprint: sprintId === null ? null : { id: sprintId, name: 'Sprint', status: 'current' },
    parent: null,
    points,
    revision: 7,
    children: children.map(childOf),
  }) as unknown as WorkItemDto;

const tasks141 = [
  taskItem('SHIP-171', 'API lookup', 'completed', { parent: 'SHIP-141', sprint: 's24' }),
  taskItem('SHIP-172', 'Card picker', 'inProgress', { parent: 'SHIP-141', sprint: 's24' }),
];
const looseTask = taskItem('SHIP-182', 'Update the runbook', 'proposed', { sprint: 's24' });
const items = [
  story('SHIP-141', 'Pay with saved card', 5, 's24', tasks141),
  ...tasks141,
  story('SHIP-138', 'Totals wrong', 2, 's24', [], 'bug'),
  story('SHIP-151', 'Other sprint', 5, 's25'),
  story('SHIP-156', 'Backlog item', 8, null),
  looseTask,
];

const metrics: SprintMetricsDto = {
  sprintId: 's24',
  committed: 7,
  completed: 2,
  remaining: 5,
  burndown: [
    { date: '2026-10-19', remaining: 7, ideal: 7 },
    { date: '2026-10-20', remaining: 7, ideal: 5.25 },
    { date: '2026-10-21', remaining: 5, ideal: 3.5 },
  ],
};

const capacity = (total: number | null): SprintCapacityDto => ({
  sprintId: 's24',
  members: total === null ? [] : [{ user: priya, points: total, note: null }],
  teamTotal: total ?? 0,
});

const states = ['story', 'bug', 'task'].flatMap((type) =>
  (
    [
      ['New', 'proposed'],
      ['Active', 'inProgress'],
      ['Closed', 'completed'],
    ] as const
  ).map(([name, category], order) => ({
    id: `${type}-${name}`,
    type,
    name,
    category,
    order,
    itemCount: 0,
  })),
);

const members = [priya, { ...priya, id: 'u2', name: 'Sam', initials: 'S' }].map((user) => ({
  user,
  email: `${user.id}@x`,
  role: 'member',
  status: 'active',
}));

async function stub(page: Page, entered: number | null = 10) {
  await page.route('**/api/sprints/s24/metrics', (route) => route.fulfill({ json: metrics }));
  await page.route('**/api/sprints/s24/capacity', (route) =>
    route.fulfill({ json: capacity(entered) }),
  );
  await page.route('**/api/projects/p1/velocity', (route) =>
    route.fulfill({ json: { window: 3, sprints: [], average: 19, override: null } }),
  );
  await page.route('**/api/projects/p1/work-items', (route) => route.fulfill({ json: items }));
  await page.route('**/api/projects/p1/states', (route) => route.fulfill({ json: states }));
  await page.route('**/api/projects/p1/members', (route) => route.fulfill({ json: members }));
  await page.route('**/api/me', (route) => route.fulfill({ json: { ...priya, email: 'p@x' } }));
}

test('SprintBacklog_WhenLoaded_ShouldShowCommittedAgainstCapacityAndTheServersFigures', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.kpiCommitted)).toHaveText('7/ 10 pts');
  await expect(page.getByTestId(ids.kpiCompleted)).toHaveText('2pts');
  await expect(page.getByTestId(ids.kpiRemaining)).toHaveText('5pts');
  await expect(page.getByTestId(ids.kpiDaysLeft)).toHaveText('5 working days left');
});

test('SprintBacklog_WhenNoCapacityEntered_ShouldCompareToVelocityAndSaySo', async ({
  mount,
  page,
}) => {
  await stub(page, null);
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.kpiCommitted)).toHaveText('7/ 19 pts · velocity');
});

test('SprintBacklog_WhenLoaded_ShouldDrawBurndownPointsFromTheMetrics', async ({ mount, page }) => {
  await stub(page);
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  const points = page.getByTestId(ids.burndownPoint);
  await expect(points).toHaveCount(3);
  await expect(points.nth(0)).toHaveAttribute('data-day', '19');
  await expect(points.nth(2)).toHaveAttribute('data-remaining', '5');
  await expect(points.nth(2)).toHaveAttribute('data-ideal', '3.5');
  const actual = await page.getByTestId(ids.burndownActual).getAttribute('points');
  expect(actual?.split(' ')).toHaveLength(3);
  const ys = actual!.split(' ').map((pair) => Number(pair.split(',')[1]));
  expect(ys[2]!).toBeGreaterThan(ys[0]!);
  const ideal = await page.getByTestId(ids.burndownIdeal).getAttribute('points');
  expect(ideal?.split(' ')).toHaveLength(10);
  expect(ideal!.split(' ')[9]).toMatch(/^226,/);
  expect(actual!.split(' ')[2]!.split(',')[0]).not.toBe('226');
});

test('SprintBacklog_WhenLoaded_ShouldLinkCommittedToTheCapacityTab', async ({ mount, page }) => {
  await stub(page);
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.kpiCapacityEdit)).toHaveAttribute(
    'href',
    '/p/SHIP/sprints/s24/capacity',
  );
});

test('SprintBacklog_WhenAStoryHasARemovedTask_ShouldNotListOrCountIt', async ({ mount, page }) => {
  await stub(page);
  // The project's list holds no Removed items, but the story's children still name the one it had.
  const withRemoved = structuredClone(items);
  withRemoved[0]!.children.push({
    ...childOf(taskItem('SHIP-173', 'Dropped idea', 'inProgress')),
    state: state('Removed', 'removed'),
  });
  await page.route('**/api/projects/p1/work-items', (route) =>
    route.fulfill({ json: withRemoved }),
  );
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.taskRow)).toHaveCount(3);
  await expect(page.getByTestId(ids.storyRow).first().getByTestId(ids.rowTasks)).toHaveText(
    '☑ 1/2',
  );
});

test('SprintBacklog_WhenLoaded_ShouldListThisSprintsStoriesAndBugsWithTheirTasks', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.storyRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.storyRow)).toContainText([
    'Pay with saved card',
    'Totals wrong',
  ]);
  const first = page.getByTestId(ids.storyRow).first();
  await expect(first.getByTestId(ids.rowTasks)).toHaveText('☑ 1/2');
  await expect(first.getByTestId(componentTestIds.typeIcon)).toHaveAttribute('data-type', 'story');
  await expect(page.getByTestId(ids.taskRow)).toHaveText([
    /API lookup/,
    /Card picker/,
    /Update the runbook/,
  ]);
});

test('SprintBacklog_WhenToggled_ShouldHideTheStoryTasksOrAllOfThem', async ({ mount, page }) => {
  await stub(page);
  await mount(<SprintBacklog project={project} sprint={sprint} />);
  await expect(page.getByTestId(ids.taskRow)).toHaveCount(3);

  await page.getByTestId(ids.rowToggle).first().click();
  await expect(page.getByTestId(ids.taskRow)).toHaveCount(1);
  await page.getByTestId(ids.rowToggle).first().click();
  await expect(page.getByTestId(ids.taskRow)).toHaveCount(3);

  await page.getByTestId(ids.showTasks).uncheck();
  await expect(page.getByTestId(ids.taskRow)).toHaveCount(0);
  await expect(page.getByTestId(ids.unparentedHeading)).toHaveCount(0);
  await expect(page.getByTestId(ids.storyRow)).toHaveCount(2);
});

test('SprintBacklog_WhenSprintHasNoItems_ShouldSaySo', async ({ mount, page }) => {
  await stub(page);
  await page.route('**/api/projects/p1/work-items', (route) => route.fulfill({ json: [] }));
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.tableEmpty)).toBeVisible();
});

test('SprintBacklog_WhenMetricsFail_ShouldShowTheLoadError', async ({ mount, page }) => {
  await stub(page);
  await page.route('**/api/sprints/s24/metrics', (route) =>
    route.fulfill({ status: 500, json: { error: 'boom', code: 'internal' } }),
  );
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.backlogLoadError)).toBeVisible();
});

test('SprintBacklog_WhenLoaded_ShouldListUnparentedTasksLastUnderTheirOwnHeading', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.unparentedHeading)).toHaveText('Tasks without a story');
  await expect(page.getByTestId(ids.taskRow).last()).toContainText('SHIP-182');
  const rows = page.locator(
    `[data-testid="${ids.storyRow}"], [data-testid="${ids.taskRow}"], [data-testid="${ids.unparentedHeading}"]`,
  );
  await expect(rows.last()).toHaveAttribute('data-key', 'SHIP-182');
  await expect(rows.nth(-2)).toHaveAttribute('data-testid', ids.unparentedHeading);
});

test('SprintBacklog_WhenOnlyStoriesAreInTheSprint_ShouldShowNoUnparentedHeading', async ({
  mount,
  page,
}) => {
  await stub(page);
  await page.route('**/api/projects/p1/work-items', (route) =>
    route.fulfill({ json: items.filter((i) => i.key !== 'SHIP-182') }),
  );
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.storyRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.unparentedHeading)).toHaveCount(0);
});

test('SprintBacklog_WhenLoaded_ShouldShowTheRailReadyActiveAndPointsByState', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.railReadyItem)).toHaveCount(1);
  await expect(page.getByTestId(ids.railReadyItem)).toContainText('Update the runbook');
  const people = page.getByTestId(ids.railPerson);
  await expect(people).toHaveCount(2);
  await expect(people.nth(0)).toContainText('(you)');
  await expect(people.nth(0).getByTestId(ids.railPersonActive)).toHaveText('3 active');
  await expect(people.nth(1).getByTestId(ids.railPersonActive)).toHaveText('—');
  const points = page.getByTestId(ids.railPoints);
  await expect(points.filter({ has: page.locator('[data-category="inProgress"]') })).toHaveText(
    /In progress 7/,
  );
  await expect(points.filter({ has: page.locator('[data-category="completed"]') })).toHaveText(
    /Completed 0/,
  );
});

test('SprintBacklog_WhenNothingIsWaiting_ShouldSayTheRailIsEmpty', async ({ mount, page }) => {
  await stub(page);
  await page.route('**/api/projects/p1/work-items', (route) =>
    route.fulfill({ json: items.filter((i) => i.key !== 'SHIP-182') }),
  );
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.railReadyEmpty)).toHaveText('Nothing waiting.');
});

interface Sent {
  url: string;
  body: { expectedRevision: number; changes: { state: { value: string } } };
}

// A PATCH that applies the new state to the list when the revision matches (4 for the tasks) and answers 409 otherwise.
async function stubPatch(page: Page, sent: Sent[], notices: string[] = []) {
  const live = structuredClone(items);
  const reads = { items: 0, metrics: 0, capacity: 0 };
  await page.route('**/api/projects/p1/work-items', (route) => {
    reads.items += 1;
    return route.fulfill({ json: live });
  });
  await page.route('**/api/sprints/s24/metrics', (route) => {
    reads.metrics += 1;
    return route.fulfill({ json: metrics });
  });
  await page.route('**/api/sprints/s24/capacity', (route) => {
    reads.capacity += 1;
    return route.fulfill({ json: capacity(10) });
  });
  await page.route('**/api/work-items/*', (route) => {
    const body = route.request().postDataJSON() as Sent['body'];
    sent.push({ url: route.request().url(), body });
    const target = live.find((i) => route.request().url().endsWith(`/${i.id}`))!;
    if (body.expectedRevision !== target.revision) {
      return route.fulfill({
        status: 409,
        json: { error: 'changed', code: 'conflict', current: target, theirChanges: [] },
      });
    }
    const picked = states.find((s) => s.id === body.changes.state.value)!;
    target.state = { id: picked.id, name: picked.name, category: picked.category };
    target.revision += 1;
    return route.fulfill({
      json: { id: target.id, key: target.key, revision: target.revision, notices },
    });
  });
  return { live, reads };
}

test('SprintBacklog_WhenAStateIsPicked_ShouldPatchWithTheLoadedRevisionAndRefreshEverything', async ({
  mount,
  page,
}) => {
  await stub(page);
  const sent: Sent[] = [];
  const { reads } = await stubPatch(page, sent);
  await mount(<SprintBacklog project={project} sprint={sprint} />);
  const row = page.getByTestId(ids.taskRow).filter({ hasText: 'SHIP-172' });
  await expect(row).toBeVisible();
  const before = { ...reads };

  await row.getByTestId(ids.stateButton).click();
  await expect(page.getByTestId(componentTestIds.stateMenuOption)).toHaveText([
    /New/,
    /Active/,
    /Closed/,
  ]);
  await page.getByTestId(componentTestIds.stateMenuOption).filter({ hasText: 'Closed' }).click();

  await expect(row.getByTestId(componentTestIds.stateDot)).toHaveText('Closed');
  expect(sent).toHaveLength(1);
  expect(sent[0]!.url).toMatch(/\/api\/work-items\/SHIP-172$/);
  expect(sent[0]!.body.expectedRevision).toBe(4);
  expect(sent[0]!.body.changes.state.value).toBe('task-Closed');
  await expect(page.getByTestId(ids.stateNotice)).toBeHidden();
  expect(reads.items).toBeGreaterThan(before.items);
  expect(reads.metrics).toBeGreaterThan(before.metrics);
  expect(reads.capacity).toBeGreaterThan(before.capacity);
});

test('SprintBacklog_WhenAStateIsPickedTwice_ShouldSendTheRevisionTheFirstWriteReturned', async ({
  mount,
  page,
}) => {
  await stub(page);
  const sent: Sent[] = [];
  await stubPatch(page, sent);
  await mount(<SprintBacklog project={project} sprint={sprint} />);
  const row = page.getByTestId(ids.taskRow).filter({ hasText: 'SHIP-172' });
  const pick = async (name: string) => {
    await row.getByTestId(ids.stateButton).click();
    await page.getByTestId(componentTestIds.stateMenuOption).filter({ hasText: name }).click();
    await expect(row.getByTestId(componentTestIds.stateDot)).toHaveText(name);
  };

  await pick('Closed');
  await pick('New');

  expect(sent.map((s) => s.body.expectedRevision)).toEqual([4, 5]);
});

test('SprintBacklog_WhenTheStateChangeIsStale_ShouldRefetchTheRowAndSaySo', async ({
  mount,
  page,
}) => {
  await stub(page);
  const sent: Sent[] = [];
  const { live } = await stubPatch(page, sent);
  await mount(<SprintBacklog project={project} sprint={sprint} />);
  const row = page.getByTestId(ids.taskRow).filter({ hasText: 'SHIP-172' });
  await expect(row.getByTestId(componentTestIds.stateDot)).toHaveText('Active');
  // Someone else moved it after this tab loaded the list.
  const target = live.find((i) => i.key === 'SHIP-172')!;
  target.state = { id: 'task-New', name: 'New', category: 'proposed' };
  target.revision = 5;

  await row.getByTestId(ids.stateButton).click();
  await page.getByTestId(componentTestIds.stateMenuOption).filter({ hasText: 'Closed' }).click();

  await expect(page.getByTestId(ids.stateNotice)).toContainText(
    'SHIP-172 was changed by someone else',
  );
  await expect(row.getByTestId(componentTestIds.stateDot)).toHaveText('New');
  expect(sent).toHaveLength(1);
  expect(sent[0]!.body.expectedRevision).toBe(4);
});

test('SprintBacklog_WhenTheStateChangeFails_ShouldShowTheServersMessage', async ({
  mount,
  page,
}) => {
  await stub(page);
  await page.route('**/api/work-items/*', (route) =>
    route.fulfill({ status: 422, json: { error: 'Close the open tasks first.', code: 'blocked' } }),
  );
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await page.getByTestId(ids.storyRow).first().getByTestId(ids.stateButton).click();
  await page.getByTestId(componentTestIds.stateMenuOption).filter({ hasText: 'Closed' }).click();

  await expect(page.getByTestId(ids.stateNotice)).toHaveText('Close the open tasks first.');
});

test('SprintBacklog_WhenTheCurrentStateIsPicked_ShouldSendNothing', async ({ mount, page }) => {
  await stub(page);
  const sent: Sent[] = [];
  await stubPatch(page, sent);
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await page.getByTestId(ids.storyRow).first().getByTestId(ids.stateButton).click();
  await page.getByTestId(componentTestIds.stateMenuOption).filter({ hasText: 'Active' }).click();

  await expect(page.getByTestId(componentTestIds.stateMenu)).toHaveCount(0);
  expect(sent).toHaveLength(0);
});

test('SprintBacklog_WhenThePickAssignedTheMover_ShouldToastAssignedToYou', async ({
  mount,
  page,
}) => {
  await stub(page);
  await stubPatch(page, [], ['pulled']);
  await mount(
    <>
      <SprintBacklog project={project} sprint={sprint} />
      <ToastHost />
    </>,
  );
  const row = page.getByTestId(ids.taskRow).filter({ hasText: 'SHIP-182' });
  await row.getByTestId(ids.stateButton).click();
  await page.getByTestId(componentTestIds.stateMenuOption).filter({ hasText: 'Active' }).click();

  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Assigned to you');
});

test('SprintBacklog_WhenThePickAssignedNobody_ShouldNotToast', async ({ mount, page }) => {
  await stub(page);
  await stubPatch(page, []);
  await mount(
    <>
      <SprintBacklog project={project} sprint={sprint} />
      <ToastHost />
    </>,
  );
  const row = page.getByTestId(ids.taskRow).filter({ hasText: 'SHIP-172' });
  await row.getByTestId(ids.stateButton).click();
  await page.getByTestId(componentTestIds.stateMenuOption).filter({ hasText: 'Closed' }).click();
  await expect(row.getByTestId(componentTestIds.stateDot)).toHaveText('Closed');

  await expect(page.getByTestId(componentTestIds.toast)).toHaveCount(0);
});

test('SprintBacklog_WhenPickedAgainBeforeTheRefetchLands_ShouldNotSendAStaleRevision', async ({
  mount,
  page,
}) => {
  await stub(page);
  const sent: Sent[] = [];
  const { live } = await stubPatch(page, sent);
  await mount(<SprintBacklog project={project} sprint={sprint} />);
  const row = page.getByTestId(ids.taskRow).filter({ hasText: 'SHIP-172' });
  await expect(row.getByTestId(componentTestIds.stateDot)).toHaveText('Active');
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/projects/p1/work-items', async (route) => {
    await gate;
    await route.fulfill({ json: live });
  });

  await row.getByTestId(ids.stateButton).click();
  await page.getByTestId(componentTestIds.stateMenuOption).filter({ hasText: 'Closed' }).click();
  await expect.poll(() => sent.length).toBe(1);
  try {
    await row.getByTestId(ids.stateButton).click({ timeout: 1500 });
    await page
      .getByTestId(componentTestIds.stateMenuOption)
      .filter({ hasText: 'New' })
      .click({ timeout: 1500 });
  } catch {
    // the guard held: the button is disabled until the refetch lands
  }
  release();
  await expect(row.getByTestId(componentTestIds.stateDot)).not.toHaveText('Active');
  await page.waitForLoadState('networkidle');
  await expect(page.getByTestId(ids.stateNotice)).toBeHidden();
  expect(sent.filter((s) => s.body.expectedRevision === 4)).toHaveLength(1);
});

test('SprintBacklog_WhenATaskIsInThisSprintUnderAStoryOfAnother_ShouldListItWithTheUnparentedOnes', async ({
  mount,
  page,
}) => {
  await stub(page);
  const inOther = structuredClone(items);
  // SHIP-171 stays in this sprint under SHIP-141, whose sprint is moved away; SHIP-172 moves to the other sprint.
  inOther.find((i) => i.key === 'SHIP-141')!.sprint = {
    id: 's25',
    name: 'Sprint',
    status: 'current',
  };
  inOther.find((i) => i.key === 'SHIP-172')!.sprint = {
    id: 's25',
    name: 'Sprint',
    status: 'current',
  };
  await page.route('**/api/projects/p1/work-items', (route) => route.fulfill({ json: inOther }));
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await expect(page.getByTestId(ids.storyRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.taskRow)).toHaveText([/API lookup/, /Update the runbook/]);
  await expect(page.getByTestId(ids.unparentedHeading)).toBeVisible();
});

// Scrolls the button into view and lets that scroll land first: any scroll closes an open menu.
async function openStateMenu(page: Page, button: Locator) {
  await button.scrollIntoViewIfNeeded();
  await page.evaluate(
    () => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))),
  );
  await button.click();
  await expect(page.getByTestId(componentTestIds.stateMenuOption).first()).toBeVisible();
}

test('SprintBacklog_WhenNarrow_ShouldScrollTheTableAndKeepTheStateMenuVisible', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 420, height: 1600 });
  await stub(page);
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  const table = page.getByTestId(ids.table);
  const scrolls = await table.evaluate((node) => {
    const wrapper = node.parentElement!;
    return (
      wrapper.scrollWidth > wrapper.clientWidth && getComputedStyle(wrapper).overflowX === 'auto'
    );
  });
  expect(scrolls).toBe(true);
  // The last row's menu opens below the table, where the wrapper's own overflow would clip it without the portal.
  await openStateMenu(page, page.getByTestId(ids.taskRow).last().getByTestId(ids.stateButton));
  const options = page.getByTestId(componentTestIds.stateMenuOption);
  await expect(options.first()).toBeVisible();
  const wrapper = await table.evaluate((node) => {
    const rect = node.parentElement!.getBoundingClientRect();
    return { top: rect.top, bottom: rect.bottom };
  });
  const box = (await options.last().boundingBox())!;
  const list = (await page.getByTestId(componentTestIds.stateMenu).boundingBox())!;
  // The menu reaches past the wrapper that scrolls the table, so without the portal it would be cut off.
  expect(list.y < wrapper.top || list.y + list.height > wrapper.bottom).toBe(true);
  expect(box.y).toBeGreaterThan(0);
  await expect(options.last()).toBeInViewport({ ratio: 1 });
});

test('SprintBacklog_WhenStateMenuClosesWithEscOrTab_ShouldReturnFocusToItsButton', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SprintBacklog project={project} sprint={sprint} />);
  const button = page.getByTestId(ids.taskRow).first().getByTestId(ids.stateButton);

  await button.click();
  await expect(page.getByTestId(componentTestIds.stateMenuOption).first()).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(componentTestIds.stateMenuOption)).toHaveCount(0);
  await expect(button).toBeFocused();

  await button.click();
  await expect(page.getByTestId(componentTestIds.stateMenuOption).first()).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(page.getByTestId(componentTestIds.stateMenuOption)).toHaveCount(0);
  // Tab carries on from the button (the next control in the page), not from the end of the document where the menu was mounted.
  const next = await page.evaluate(() => document.activeElement?.tagName);
  expect(next).not.toBe('BODY');
});

test('SprintBacklog_WhenThePageScrollsWithAStateMenuOpen_ShouldCloseIt', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 420, height: 300 });
  await stub(page);
  await mount(<SprintBacklog project={project} sprint={sprint} />);

  await openStateMenu(page, page.getByTestId(ids.taskRow).first().getByTestId(ids.stateButton));
  await page.evaluate(() => window.scrollBy(0, 40));

  await expect(page.getByTestId(componentTestIds.stateMenuOption)).toHaveCount(0);
});

test('SprintBacklog_WhenStoryTitleClicked_ShouldOpenItemModal', async ({ mount, page }) => {
  await stub(page);
  await mount(
    <>
      <SprintBacklog project={project} sprint={sprint} />
      <AddressReadout />
    </>,
    { hooksConfig: { route: '/p/SHIP/sprints/s24/backlog' } },
  );

  const first = page.getByTestId(ids.storyRow).first();
  const link = first.getByRole('link');
  await expect(link).toHaveAttribute('href', /\/p\/SHIP\/items\/SHIP-/);
  await link.click();

  await expect(page.getByTestId('address')).toHaveText(
    /^\/p\/SHIP\/sprints\/s24\/backlog\?item=SHIP-/,
  );
  await expect(first.getByTestId(ids.rowToggle)).toHaveAttribute('aria-expanded', 'true');
});
