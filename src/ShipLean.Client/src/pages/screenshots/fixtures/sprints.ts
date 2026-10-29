import type {
  MemberCapacityDto,
  RolloverDto,
  RolloverItemDto,
  SprintCapacityDto,
  SprintDto,
  SprintMetricsDto,
  VelocityDto,
} from '../../../api/sprints';
import type { StateDto } from '../../../api/projects';
import type { StateCategory, UserRef } from '../../../api/refs';
import type { ChildSummaryDto, WorkItemDto } from '../../../api/workItems';
import { PEOPLE, SHELL_SPRINTS } from '../shellStub';
import type { Page } from '../shoot';

const { priya, tom, mei, sam, ana } = PEOPLE;

export const SPRINT_24 = 'sprint-24';

// ---- All sprints (design/screens/sprints.html) ----

const figures: Record<string, { capacity: number | null; committed: number; completed: number }> = {
  'sprint-21': { capacity: 16, committed: 20, completed: 20 },
  'sprint-22': { capacity: 24, committed: 24, completed: 22 },
  'sprint-23': { capacity: 23, committed: 23, completed: 21 },
  'sprint-24': { capacity: 23, committed: 21, completed: 5 },
  'sprint-25': { capacity: 22, committed: 13, completed: 0 },
  'sprint-26': { capacity: null, committed: 0, completed: 0 },
};

const GOALS: Record<string, string> = {
  'sprint-21': 'Reporting MVP',
  'sprint-22': 'Checkout performance',
  'sprint-23': 'Coupons v2',
  'sprint-24': 'Customers can check out with a saved card; passkey sign-in behind a flag.',
  'sprint-25': 'Remember-me and the reporting export',
};

export const sprintsWithGoals: SprintDto[] = SHELL_SPRINTS.map((s) => ({
  ...s,
  goal: GOALS[s.id] ?? '',
}));

const capacityOf = (sprintId: string, total: number | null): SprintCapacityDto => ({
  sprintId,
  members: total === null ? [] : [{ user: priya, days: null, points: total, note: null }],
  teamTotal: total ?? 0,
  velocity: null,
  velocityDefault: 0,
  workingDays: 10,
  defaultDays: 10,
  holidays: [],
  holidayCountry: null,
  ended: false,
});

const velocity: VelocityDto = {
  window: 3,
  average: 21,
  override: null,
  sprints: [
    { sprintId: 'sprint-19', sprintName: 'Sprint 19', committed: 18, completed: 16 },
    { sprintId: 'sprint-20', sprintName: 'Sprint 20', committed: 22, completed: 19 },
    { sprintId: 'sprint-21', sprintName: 'Sprint 21', committed: 20, completed: 20 },
    { sprintId: 'sprint-22', sprintName: 'Sprint 22', committed: 24, completed: 22 },
    { sprintId: 'sprint-23', sprintName: 'Sprint 23', committed: 23, completed: 21 },
  ],
};

const sprintIdOf = (url: string, fromEnd: number) => url.split('/').at(-fromEnd)!;

async function stubSprintFigures(page: Page, burndown: SprintMetricsDto['burndown'] = []) {
  await page.route('**/api/projects/p1/velocity', (route) => route.fulfill({ json: velocity }));
  await page.route('**/api/sprints/*/metrics', (route) => {
    const id = sprintIdOf(route.request().url(), 2);
    const f = figures[id] ?? { capacity: null, committed: 0, completed: 0 };
    return route.fulfill({
      json: {
        sprintId: id,
        committed: f.committed,
        completed: f.completed,
        remaining: f.committed - f.completed,
        burndown: id === SPRINT_24 ? burndown : [],
      } satisfies SprintMetricsDto,
    });
  });
  await page.route('**/api/sprints/*/capacity', (route) => {
    const id = sprintIdOf(route.request().url(), 2);
    return route.fulfill({ json: capacityOf(id, figures[id]?.capacity ?? null) });
  });
}

export async function stubSprintsList(page: Page) {
  await stubSprintFigures(page);
  await page.route('**/api/projects/p1/sprints/next', (route) =>
    route.fulfill({
      json: { name: 'Sprint 27', startDate: '2026-11-30', endDate: '2026-12-11', goal: '' },
    }),
  );
}

// ---- Sprint backlog (design/screens/sprint-backlog.html) ----

const stateOf = (name: string, category: StateCategory, type: string) => ({
  id: `${type}-${name}`,
  name,
  category,
});

const STATE_ROWS: [string, StateCategory][] = [
  ['New', 'proposed'],
  ['Active', 'inProgress'],
  ['In review', 'resolved'],
  ['Closed', 'completed'],
];

export const states: StateDto[] = ['story', 'bug', 'task'].flatMap((type) =>
  STATE_ROWS.map(([name, category], order) => ({
    id: `${type}-${name}`,
    type: type as StateDto['type'],
    name,
    category,
    order,
    itemCount: 0,
  })),
);

const CATEGORY_OF: Record<string, StateCategory> = Object.fromEntries(STATE_ROWS);

let nextNumber = 300;
function item(
  type: 'story' | 'bug' | 'task',
  key: string | null,
  title: string,
  state: string,
  assignee: UserRef | null,
  points: number | null,
  sprintId: string,
  parent?: WorkItemDto,
  children: WorkItemDto[] = [],
): WorkItemDto {
  nextNumber += 1;
  const id = key ?? `SHIP-${nextNumber}`;
  const sprintStatus = SHELL_SPRINTS.find((s) => s.id === sprintId)?.status ?? 'current';
  return {
    id,
    projectId: 'p1',
    key: id,
    number: Number(id.slice(5)),
    type: { id: type, name: type === 'bug' ? 'Bug' : type === 'task' ? 'Task' : 'Story' },
    title,
    description: '',
    descriptionHtml: '',
    acceptanceCriteria: [],
    reproSteps: '',
    reproStepsHtml: '',
    severity: null,
    state: { ...stateOf(state, CATEGORY_OF[state]!, type) },
    assignee,
    createdBy: null,
    changedBy: null,
    sprint: { id: sprintId, name: sprintId.replace('sprint-', 'Sprint '), status: sprintStatus },
    priority: 2,
    points,
    parent:
      parent === undefined
        ? null
        : { id: parent.id, key: parent.key, type: parent.type, title: parent.title },
    tags: [],
    position: { columnId: null, done: false, laneId: null, columnOrder: null },
    backlogOrder: id,
    links: [],
    children: children.map((c): ChildSummaryDto => ({
      id: c.id,
      key: c.key,
      type: c.type,
      title: c.title,
      state: c.state,
      assignee: c.assignee,
      points: c.points,
    })),
    originalSystem: null,
    revision: 3,
    createdAt: '2026-10-01T09:00:00Z',
    createdAtLabel: '3 weeks ago',
    changedAt: '2026-10-20T09:00:00Z',
    changedAtLabel: '5 days ago',
    goal: '',
    statusLine: '',
    statusVerified: null,
    slug: null,
    startedOn: null,
    startedLabel: null,
  };
}

function story(
  key: string,
  title: string,
  state: string,
  assignee: UserRef,
  points: number,
  tasks: [string, string, UserRef | null][],
  type: 'story' | 'bug' = 'story',
  sprintId = SPRINT_24,
): WorkItemDto[] {
  const shell = item(type, key, title, state, assignee, points, sprintId);
  const taskItems = tasks.map(([t, s, who]) =>
    item('task', null, t, s, who, null, sprintId, shell),
  );
  return [
    item(type, key, title, state, assignee, points, sprintId, undefined, taskItems),
    ...taskItems,
  ];
}

export const sprintItems: WorkItemDto[] = [
  ...story('SHIP-141', 'Pay with saved card', 'Active', priya, 5, [
    ['API: tokenised card lookup', 'In review', priya],
    ['UI: saved card picker', 'Active', mei],
    ['E2E test', 'New', null],
  ]),
  ...story(
    'SHIP-138',
    'Totals wrong when coupon removed',
    'Closed',
    tom,
    2,
    [['Fix and regression test', 'Closed', tom]],
    'bug',
  ),
  ...story('SHIP-144', 'Address autocomplete', 'Active', mei, 3, [
    ['Provider spike', 'Closed', mei],
    ['Wire into the address form', 'New', null],
  ]),
  ...story('SHIP-129', 'Sign in with passkey', 'Active', sam, 8, [
    ['WebAuthn registration', 'Closed', sam],
    ['WebAuthn sign-in', 'Active', sam],
    ['Fallback to password', 'New', null],
    ['Feature flag', 'Closed', ana],
  ]),
  ...story('SHIP-150', 'Order confirmation email', 'Closed', ana, 3, [
    ['Email template', 'Closed', ana],
  ]),
];

const burndown: SprintMetricsDto['burndown'] = [
  { date: '2026-10-19', remaining: 21, ideal: 21 },
  { date: '2026-10-20', remaining: 21, ideal: 18.7 },
  { date: '2026-10-21', remaining: 19, ideal: 16.3 },
  { date: '2026-10-22', remaining: 19, ideal: 14 },
  { date: '2026-10-23', remaining: 16, ideal: 11.7 },
];

export async function stubSprintBacklog(page: Page) {
  await stubSprintFigures(page, burndown);
  await page.route('**/api/projects/p1/work-items', (route) =>
    route.fulfill({ json: sprintItems }),
  );
  await page.route('**/api/projects/p1/states', (route) => route.fulfill({ json: states }));
}

// ---- Capacity (design/screens/capacity.html) ----

const entry = (user: UserRef, points: number, note: string | null): MemberCapacityDto => ({
  user,
  days: null,
  points,
  note,
});

const capacitySprint24: SprintCapacityDto = {
  sprintId: SPRINT_24,
  members: [
    entry(priya, 6, null),
    entry(tom, 5, null),
    entry(mei, 5, null),
    entry(sam, 4, 'Off Thu–Fri'),
    entry(ana, 3, 'Part-time this sprint'),
  ],
  teamTotal: 23,
  velocity: null,
  velocityDefault: 0,
  workingDays: 10,
  defaultDays: 10,
  holidays: [],
  holidayCountry: null,
  ended: false,
};

export async function stubCapacity(page: Page) {
  await stubSprintFigures(page);
  await page.route('**/api/sprints/sprint-24/capacity', (route) =>
    route.fulfill({ json: capacitySprint24 }),
  );
}

// ---- Sprint end (design/screens/sprint-end.html) ----

/** Sprints 21–23 decided, Sprint 24 ended with its rollover pending, Sprint 25 now current. */
export const sprintsAtEnd: SprintDto[] = sprintsWithGoals.map((s) => {
  if (s.id === 'sprint-24')
    return { ...s, status: 'past', rolloverStatus: 'pending', timeLabel: 'Ended today' };
  if (s.id === 'sprint-25') return { ...s, status: 'current', timeLabel: '10 days left' };
  if (s.status === 'past') return { ...s, rolloverStatus: 'decided' };
  return s;
});

const unfinished = (
  key: string,
  title: string,
  assignee: UserRef,
  points: number,
  open: number,
  total: number,
): RolloverItemDto => ({
  workItemId: key,
  key,
  title,
  type: 'story',
  state: { id: 'story-Active', name: 'Active', category: 'inProgress' },
  assignee,
  points,
  openTasks: open,
  totalTasks: total,
  defaultTargetSprintId: 'sprint-25',
});

const rollover: RolloverDto = {
  sprint: { id: SPRINT_24, name: 'Sprint 24', status: 'past' },
  status: 'pending',
  items: [
    unfinished('SHIP-144', 'Address autocomplete', mei, 3, 1, 2),
    unfinished('SHIP-129', 'Sign in with passkey', sam, 8, 2, 4),
  ],
  nextSprint: { id: 'sprint-25', name: 'Sprint 25', status: 'current' },
  nextSprintCapacity: 22,
  nextSprintPoints: 13,
};

export async function stubSprintEnd(page: Page) {
  await stubSprintFigures(page);
  await page.route('**/api/sprints/sprint-24/metrics', (route) =>
    route.fulfill({
      json: {
        sprintId: SPRINT_24,
        committed: 21,
        completed: 10,
        remaining: 11,
        burndown: [],
      } satisfies SprintMetricsDto,
    }),
  );
  await page.route('**/api/sprints/sprint-24/rollover', (route) =>
    route.fulfill({ json: rollover }),
  );
  await page.route('**/api/projects/p1/sprints/next', (route) =>
    route.fulfill({
      json: { name: 'Sprint 27', startDate: '2026-11-30', endDate: '2026-12-11', goal: '' },
    }),
  );
}
