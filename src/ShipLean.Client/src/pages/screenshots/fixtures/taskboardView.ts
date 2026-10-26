import type { StateRef, TypeRef, UserRef } from '../../../api/refs';
import type { CardDto, TaskboardCellDto, TaskboardViewDto } from '../../../api/views';
import type { CardFieldSettingJson, StyleRuleJson } from '../../../api/persisted';
import { PEOPLE } from '../shellStub';

// Sprint 24's Taskboard as design/screens/taskboard.html draws it: five stories and bugs, four Task states.
const NEW: StateRef = { id: 't-new', name: 'New', category: 'proposed' };
const ACTIVE: StateRef = { id: 't-active', name: 'Active', category: 'inProgress' };
const REVIEW: StateRef = { id: 't-review', name: 'In review', category: 'resolved' };
const CLOSED: StateRef = { id: 't-closed', name: 'Closed', category: 'completed' };
const STATES = [NEW, ACTIVE, REVIEW, CLOSED];

const PS_ACTIVE: StateRef = { id: 'ps-active', name: 'Active', category: 'inProgress' };
const PS_CLOSED: StateRef = { id: 'ps-closed', name: 'Closed', category: 'completed' };
const STORY: TypeRef = { id: 'story', name: 'User Story' };
const BUG: TypeRef = { id: 'bug', name: 'Bug' };
const TASK: TypeRef = { id: 'task', name: 'Task' };

function card(key: string, type: TypeRef, state: StateRef, over: Partial<CardDto>): CardDto {
  return {
    id: `id-${key}`,
    key,
    type,
    title: key,
    state,
    assignee: null,
    points: null,
    childPoints: null,
    sumWarn: false,
    parent: null,
    tags: [],
    rollups: [],
    offerDone: false,
    styleRuleId: null,
    extraFields: [],
    revision: 1,
    ...over,
  };
}

const parentOf = (p: CardDto) => ({ id: p.id, key: p.key, type: p.type, title: p.title });
const rollup = (open: number, closed: number) => [{ type: 'task' as const, open, closed }];

const saved = card('SHIP-141', STORY, PS_ACTIVE, {
  title: 'Pay with saved card',
  points: 5,
  assignee: PEOPLE.priya,
  rollups: rollup(3, 0),
});
const totals = card('SHIP-138', BUG, PS_CLOSED, {
  title: 'Totals wrong when coupon removed',
  rollups: rollup(0, 1),
});
const address = card('SHIP-144', STORY, PS_ACTIVE, {
  title: 'Address autocomplete',
  points: 3,
  assignee: PEOPLE.mei,
  rollups: rollup(1, 1),
});
const passkey = card('SHIP-129', STORY, PS_ACTIVE, {
  title: 'Sign in with passkey',
  points: 8,
  assignee: PEOPLE.sam,
  rollups: rollup(2, 2),
});
const confirmation = card('SHIP-133', STORY, PS_CLOSED, {
  title: 'Order confirmation email',
  points: 2,
  rollups: rollup(0, 1),
});

const BLOCKED_RULE = 'rule-blocked';
const PRIORITY_RULE = 'rule-priority';

const task = (key: string, state: StateRef, title: string, over: Partial<CardDto> = {}) =>
  card(key, TASK, state, { title, ...over });
const t173 = task('SHIP-173', NEW, 'E2E test', { parent: parentOf(saved) });
const t172 = task('SHIP-172', ACTIVE, 'UI: saved card picker', {
  parent: parentOf(saved),
  assignee: PEOPLE.mei,
  styleRuleId: PRIORITY_RULE,
});
const t171 = task('SHIP-171', REVIEW, 'API: tokenised card lookup', {
  parent: parentOf(saved),
  assignee: PEOPLE.priya,
});
const t174 = task('SHIP-174', CLOSED, 'Fix and regression test', {
  parent: parentOf(totals),
  assignee: PEOPLE.tom,
});
const t176 = task('SHIP-176', NEW, 'Wire into the address form', { parent: parentOf(address) });
const t175 = task('SHIP-175', CLOSED, 'Provider spike', {
  parent: parentOf(address),
  assignee: PEOPLE.mei,
  tags: [{ name: 'Spike', colour: 'info' }],
});
const t179 = task('SHIP-179', NEW, 'Fallback to password', {
  parent: parentOf(passkey),
  styleRuleId: BLOCKED_RULE,
  tags: [{ name: 'Blocked', colour: 'danger' }],
});
const t178 = task('SHIP-178', ACTIVE, 'WebAuthn sign-in', {
  parent: parentOf(passkey),
  assignee: PEOPLE.sam,
});
const t177 = task('SHIP-177', CLOSED, 'WebAuthn registration', {
  parent: parentOf(passkey),
  assignee: PEOPLE.sam,
});
const t180 = task('SHIP-180', CLOSED, 'Feature flag', {
  parent: parentOf(passkey),
  assignee: PEOPLE.ana,
});
const t181 = task('SHIP-181', CLOSED, 'Send the confirmation email', {
  parent: parentOf(confirmation),
});
const t182 = task('SHIP-182', NEW, 'Rotate staging TLS certificates', {
  tags: [{ name: 'Tech debt', colour: 'warn' }],
});
const t183 = task('SHIP-183', ACTIVE, 'Update the on-call runbook', { assignee: PEOPLE.tom });

const cells = (...byState: [StateRef, CardDto[]][]): TaskboardCellDto[] =>
  STATES.map((s) => ({
    stateId: s.id,
    tasks: byState.find(([state]) => state.id === s.id)?.[1] ?? [],
  }));

const rows = [
  { parent: saved, cells: cells([NEW, [t173]], [ACTIVE, [t172]], [REVIEW, [t171]]) },
  { parent: totals, cells: cells([CLOSED, [t174]]) },
  { parent: address, cells: cells([NEW, [t176]], [CLOSED, [t175]]) },
  { parent: passkey, cells: cells([NEW, [t179]], [ACTIVE, [t178]], [CLOSED, [t177, t180]]) },
  { parent: confirmation, cells: cells([CLOSED, [t181]]) },
  { parent: null, cells: cells([NEW, [t182]], [ACTIVE, [t183]]) },
].map((row) => ({ ...row, offerResolve: false }));

const person = (p: UserRef | null, ...byState: [StateRef, CardDto[]][]) => ({
  person: p,
  taskCount: byState.reduce((n, [, tasks]) => n + tasks.length, 0),
  cells: cells(...byState),
});

export const TASKBOARD_STYLE_RULES: StyleRuleJson[] = [
  {
    id: BLOCKED_RULE,
    name: 'Blocked',
    enabled: true,
    colour: 'danger',
    bold: false,
    italic: false,
    underline: false,
    criteria: { match: 'all', clauses: [{ field: 'tags', op: 'contains', value: 'Blocked' }] },
  },
  {
    id: PRIORITY_RULE,
    name: 'Priority 1',
    enabled: true,
    colour: 'warn',
    bold: true,
    italic: false,
    underline: false,
    criteria: { match: 'all', clauses: [{ field: 'priority', op: '=', value: 1 }] },
  },
  {
    id: 'rule-review',
    name: 'Waiting on review',
    enabled: false,
    colour: 'info',
    bold: false,
    italic: false,
    underline: false,
    criteria: {
      match: 'all',
      clauses: [
        { field: 'stateCategory', op: '=', value: 'resolved' },
        { field: 'assignee', op: '=', value: '@me' },
      ],
    },
  },
];

export const TASKBOARD_VIEW: TaskboardViewDto = {
  sprintId: 'sprint-24',
  boardId: 'b-taskboard',
  columns: STATES.map((state, i) => ({
    state,
    count: rows.flatMap((r) => r.cells[i]?.tasks ?? []).length,
  })),
  rows,
  peopleRows: [
    person(null, [NEW, [t173, t176, t179, t182]]),
    person(PEOPLE.priya, [REVIEW, [t171]]),
    person(PEOPLE.tom, [ACTIVE, [t183]], [CLOSED, [t174]]),
    person(PEOPLE.mei, [ACTIVE, [t172]], [CLOSED, [t175]]),
    person(PEOPLE.sam, [ACTIVE, [t178]], [CLOSED, [t177]]),
    person(PEOPLE.ana, [CLOSED, [t180]]),
  ],
  parentStates: [
    {
      type: 'story',
      states: [{ id: 'ps-new', name: 'New', category: 'proposed' }, PS_ACTIVE, PS_CLOSED],
    },
  ],
  styleRules: TASKBOARD_STYLE_RULES,
  cardFields: [],
  annotations: { tasks: true, stories: true, bugs: true, features: true },
};

/** What the settings dialog's Fields pane shows: a task setting and a story and bug setting. */
export const TASKBOARD_CARD_FIELDS: CardFieldSettingJson[] = ['task', 'story', 'bug'].map(
  (type) => ({
    type: type as CardFieldSettingJson['type'],
    showKey: true,
    showAssignee: true,
    showTags: true,
    showPoints: type !== 'task',
    showEmptyFields: false,
    extraFields: [],
  }),
);
