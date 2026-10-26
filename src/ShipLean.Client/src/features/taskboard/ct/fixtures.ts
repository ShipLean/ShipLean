import type { ProjectDto } from '../../../api/projects';
import type { StateRef, UserRef } from '../../../api/refs';
import type { SprintDto } from '../../../api/sprints';
import type { CardDto, TaskboardRowDto, TaskboardViewDto } from '../../../api/views';

export const PROJECT: ProjectDto = {
  id: 'p1',
  key: 'SHIP',
  name: 'Shipping',
  description: '',
  role: 'admin',
  memberCount: 3,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 184,
};

export const SPRINT = {
  id: 's24',
  name: 'Sprint 24',
  status: 'current',
} as unknown as SprintDto;

export const SPRINTS: SprintDto[] = [SPRINT];

export const PRIYA: UserRef = {
  id: 'u-priya',
  name: 'Priya Shah',
  initials: 'PS',
  avatarColour: '#2a7ab5',
  photoUrl: null,
};
export const TOM: UserRef = {
  id: 'u-tom',
  name: 'Tom Okafor',
  initials: 'TO',
  avatarColour: '#1a9e8a',
  photoUrl: null,
};

const NEW: StateRef = { id: 't-new', name: 'New', category: 'proposed' };
const ACTIVE: StateRef = { id: 't-active', name: 'Active', category: 'inProgress' };
const CLOSED: StateRef = { id: 't-closed', name: 'Closed', category: 'completed' };

export function task(key: string, state: StateRef, overrides: Partial<CardDto> = {}): CardDto {
  return {
    id: `id-${key}`,
    key,
    type: { id: 'task', name: 'Task' },
    title: `Title of ${key}`,
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
    ...overrides,
  };
}

function story(key: string, state: StateRef, overrides: Partial<CardDto> = {}): CardDto {
  return task(key, state, {
    type: { id: 'story', name: 'User Story' },
    title: `Story ${key}`,
    points: 5,
    ...overrides,
  });
}

const parentRef = (p: CardDto) => ({ id: p.id, key: p.key, type: p.type, title: p.title });

const OPEN_STORY = story(
  'SHIP-141',
  { id: 'ps-active', name: 'Active', category: 'inProgress' },
  {
    rollups: [{ type: 'task', open: 2, closed: 1 }],
  },
);
const DONE_STORY = story(
  'SHIP-138',
  { id: 'ps-closed', name: 'Closed', category: 'completed' },
  {
    rollups: [{ type: 'task', open: 0, closed: 1 }],
  },
);

const e2e = task('SHIP-173', NEW, { parent: parentRef(OPEN_STORY) });
const picker = task('SHIP-172', ACTIVE, {
  parent: parentRef(OPEN_STORY),
  assignee: TOM,
  tags: [{ name: 'Spike', colour: 'info' }],
});
const api = task('SHIP-171', CLOSED, { parent: parentRef(OPEN_STORY), assignee: PRIYA });
const fix = task('SHIP-174', CLOSED, { parent: parentRef(DONE_STORY), assignee: TOM });
const loose = task('SHIP-182', NEW);

const cells = (...byState: [StateRef, CardDto[]][]) =>
  [NEW, ACTIVE, CLOSED].map((s) => ({
    stateId: s.id,
    tasks: byState.find(([state]) => state.id === s.id)?.[1] ?? [],
  }));

const rows: TaskboardRowDto[] = [
  {
    parent: OPEN_STORY,
    offerResolve: false,
    cells: cells([NEW, [e2e]], [ACTIVE, [picker]], [CLOSED, [api]]),
  },
  { parent: DONE_STORY, offerResolve: false, cells: cells([CLOSED, [fix]]) },
  { parent: null, offerResolve: false, cells: cells([NEW, [loose]]) },
];

export const VIEW: TaskboardViewDto = {
  sprintId: SPRINT.id,
  boardId: 'b1',
  columns: [NEW, ACTIVE, CLOSED].map((state) => ({
    state,
    count: rows
      .flatMap((r) => r.cells)
      .filter((c) => c.stateId === state.id)
      .flatMap((c) => c.tasks).length,
  })),
  rows,
  peopleRows: [
    { person: null, taskCount: 2, cells: cells([NEW, [e2e, loose]]) },
    { person: PRIYA, taskCount: 1, cells: cells([CLOSED, [api]]) },
    { person: TOM, taskCount: 2, cells: cells([ACTIVE, [picker]], [CLOSED, [fix]]) },
  ],
  parentStates: [
    {
      type: 'story',
      states: [
        { id: 'ps-new', name: 'New', category: 'proposed' },
        { id: 'ps-active', name: 'Active', category: 'inProgress' },
        { id: 'ps-closed', name: 'Closed', category: 'completed' },
      ],
    },
  ],
  styleRules: [],
  cardFields: [],
  annotations: { tasks: true, stories: true, bugs: true, features: true },
};
