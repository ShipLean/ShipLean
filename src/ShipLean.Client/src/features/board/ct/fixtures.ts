import type { ProjectDto } from '../../../api/projects';
import type {
  BoardCellDto,
  BoardColumnDto,
  BoardLaneDto,
  BoardViewDto,
  CardDto,
} from '../../../api/views';

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

export const BOARD_ID = 'b-stories';

export const TOM = {
  id: 'u-tom',
  name: 'Tom Okafor',
  initials: 'TO',
  avatarColour: '#2a7ab5',
  photoUrl: null,
};

const NEW = { id: 's-new', name: 'New', category: 'proposed' } as const;

export function card(overrides: Partial<CardDto> & { key: string }): CardDto {
  return {
    id: `id-${overrides.key}`,
    type: { id: 'story', name: 'User Story' },
    title: `Title of ${overrides.key}`,
    state: NEW,
    assignee: null,
    points: 3,
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

export function column(overrides: Partial<BoardColumnDto> & { id: string }): BoardColumnDto {
  return {
    name: overrides.id,
    wipLimit: null,
    count: 0,
    hiddenCount: 0,
    overLimit: false,
    split: false,
    doingCount: 0,
    doneCount: 0,
    definitionOfDone: '',
    definitionOfDoneHtml: '',
    states: [{ type: 'story', state: NEW }],
    ...overrides,
  };
}

export const cell = (
  columnId: string,
  cards: CardDto[] = [],
  extra: Partial<BoardCellDto> = {},
): BoardCellDto => ({
  columnId,
  doneHalf: false,
  cards,
  olderCount: 0,
  olderCursor: null,
  ...extra,
});

export function lane(overrides: Partial<BoardLaneDto> & { id: string }): BoardLaneDto {
  return {
    name: overrides.id,
    colour: null,
    kind: 'default',
    criteriaText: null,
    epic: null,
    acceptsDrops: true,
    cardCount: 0,
    openPoints: 0,
    cells: [],
    ...overrides,
  };
}

export const COLUMNS: BoardColumnDto[] = [
  column({ id: 'col-new', name: 'New', count: 2 }),
  column({
    id: 'col-ready',
    name: 'Ready',
    wipLimit: 8,
    count: 3,
    split: true,
    doingCount: 2,
    doneCount: 1,
    states: [
      { type: 'story', state: NEW },
      { type: 'bug', state: { id: 's-tri', name: 'Triaged', category: 'proposed' } },
    ],
  }),
  column({
    id: 'col-prog',
    name: 'In progress',
    wipLimit: 2,
    count: 3,
    overLimit: true,
    states: [{ type: 'story', state: { id: 's-act', name: 'Active', category: 'inProgress' } }],
  }),
  column({
    id: 'col-done',
    name: 'Done',
    count: 6,
    states: [{ type: 'story', state: { id: 's-closed', name: 'Closed', category: 'completed' } }],
  }),
];

export const SETTINGS = {
  styleRules: [
    {
      id: 'rule-blocked',
      name: 'Blocked',
      enabled: true,
      colour: 'danger',
      bold: true,
      italic: false,
      underline: false,
      criteria: { match: 'all', clauses: [] },
    },
  ],
  cardFields: [
    {
      type: 'story',
      showKey: true,
      showAssignee: true,
      showTags: true,
      showPoints: true,
      showEmptyFields: false,
      extraFields: [],
    },
  ],
  annotations: { tasks: true, stories: true, bugs: false, features: true },
} as const satisfies Pick<BoardViewDto, 'styleRules' | 'cardFields' | 'annotations'>;

/** The Storefront-like stories board: Expedite above the default lane, Ready split, In progress over its limit. */
export function storiesBoard(overrides: Partial<BoardViewDto> = {}): BoardViewDto {
  return {
    boardId: BOARD_ID,
    projectId: PROJECT.id,
    level: 'stories',
    openCount: 8,
    openPoints: 30,
    columns: COLUMNS,
    lanes: [
      lane({
        id: 'lane-expedite',
        name: 'Expedite',
        kind: 'criteria',
        criteriaText: 'Priority = 1',
        colour: 'warn',
        cardCount: 1,
        openPoints: 5,
        cells: [
          cell('col-new'),
          cell('col-ready'),
          cell('col-ready', [], { doneHalf: true }),
          cell('col-prog', [card({ key: 'SHIP-150', title: 'Fix payout rounding', points: 5 })]),
          cell('col-done'),
        ],
      }),
      lane({
        id: 'lane-default',
        name: 'Default lane',
        kind: 'default',
        cardCount: 2,
        openPoints: 8,
        cells: [
          cell('col-new', [card({ key: 'SHIP-160', title: 'Biometric sign-in', points: null })]),
          cell('col-ready', [card({ key: 'SHIP-161', title: 'Doing card' })]),
          cell('col-ready', [card({ key: 'SHIP-162', title: 'Done half card' })], {
            doneHalf: true,
          }),
          cell('col-prog'),
          cell('col-done', [card({ key: 'SHIP-140', title: 'Finished work' })], {
            olderCount: 2,
            olderCursor: 'done:lane-default',
          }),
        ],
      }),
    ],
    ...SETTINGS,
    ...overrides,
  };
}

/** A features board: SHIP-102 in progress with 2 of 3 stories open, SHIP-104 with every story closed (offers Done), SHIP-90 in Done with one open. */
export function featuresBoard(): BoardViewDto {
  const base = storiesBoard({ level: 'features' });
  const stories = (open: number, closed: number) => [{ type: 'story' as const, open, closed }];
  return {
    ...base,
    lanes: [
      lane({
        id: 'lane-default',
        name: 'Default lane',
        kind: 'default',
        cardCount: 3,
        cells: [
          cell('col-new', [
            card({
              key: 'SHIP-104',
              type: { id: 'feature', name: 'Feature' },
              rollups: stories(0, 3),
              offerDone: true,
            }),
          ]),
          cell('col-ready'),
          cell('col-ready', [], { doneHalf: true }),
          cell('col-prog', [
            card({
              key: 'SHIP-102',
              type: { id: 'feature', name: 'Feature' },
              rollups: stories(2, 1),
            }),
          ]),
          cell('col-done', [
            card({
              key: 'SHIP-90',
              type: { id: 'feature', name: 'Feature' },
              rollups: stories(1, 2),
            }),
          ]),
        ],
      }),
    ],
  };
}

/** A Features board for the roll-up list: one default lane with a Feature that rolls up two stories (1 closed) and a bug. */
export function rollupBoard(overrides: Partial<BoardViewDto> = {}): BoardViewDto {
  const feature = card({
    key: 'SHIP-101',
    title: 'Payout reports',
    type: { id: 'feature', name: 'Feature' },
    rollups: [
      { type: 'story', open: 2, closed: 1 },
      { type: 'bug', open: 1, closed: 0 },
    ],
  });
  return storiesBoard({
    level: 'features',
    lanes: [
      lane({
        id: 'lane-default',
        name: 'Default lane',
        cardCount: 1,
        cells: [
          cell('col-new', [feature]),
          cell('col-ready'),
          cell('col-ready', [], { doneHalf: true }),
          cell('col-prog'),
          cell('col-done'),
        ],
      }),
    ],
    annotations: { tasks: false, stories: true, bugs: true, features: true },
    ...overrides,
  });
}
