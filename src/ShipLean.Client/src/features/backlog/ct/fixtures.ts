import type { ProjectDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';

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

export const EMPTY_PROJECT: ProjectDto = { ...PROJECT, nextItemNumber: 1 };

export const TOM = {
  id: 'u-tom',
  name: 'Tom Okafor',
  initials: 'TO',
  avatarColour: '#2a7ab5',
  photoUrl: null,
};

export function itemRow(overrides: Partial<BacklogRowDto> & { key: string }): BacklogRowDto {
  return {
    kind: 'item',
    id: `id-${overrides.key}`,
    type: { id: 'story', name: 'User Story' },
    title: `Title of ${overrides.key}`,
    state: { id: 's-new', name: 'New', category: 'proposed' },
    assignee: null,
    points: 3,
    childPoints: null,
    sumWarn: false,
    sprint: null,
    parent: null,
    depth: 0,
    rank: 1,
    tags: [],
    rollups: [],
    revision: 1,
    ...overrides,
  };
}

export const ROWS: BacklogRowDto[] = [
  itemRow({
    key: 'SHIP-160',
    rank: 3,
    title: 'Biometric sign-in',
    assignee: TOM,
    points: null,
    sprint: { id: 'sp24', name: 'Sprint 24', status: 'current' },
    tags: [
      { name: 'security', colour: 'danger' },
      { name: 'spike', colour: 'purple' },
      { name: 'misc', colour: null },
    ],
    parent: {
      id: 'f1',
      key: 'SHIP-90',
      type: { id: 'feature', name: 'Feature' },
      title: 'Mobile sign-in',
    },
  }),
  itemRow({
    key: 'SHIP-158',
    rank: 7,
    type: { id: 'bug', name: 'Bug' },
    sprint: { id: 'sp25', name: 'Sprint 25', status: 'future' },
    points: 5,
  }),
];

export function backlogView(rows: BacklogRowDto[]): BacklogViewDto {
  return {
    projectId: PROJECT.id,
    level: 'stories',
    mode: 'list',
    totalCount: rows.length,
    rows,
    sections: [],
    tagColours: [],
    forecast: null,
    velocity: null,
  };
}

const CURRENT = { id: 'sp24', name: 'Sprint 24', status: 'current' } as const;
const FUTURE = { id: 'sp25', name: 'Sprint 25', status: 'future' } as const;
const LATER = { id: 'sp26', name: 'Sprint 26', status: 'future' } as const;

/**
 * Planning mode: Sprint 24 over its capacity (26 of 23), Sprint 25 with room (8 of 22), Sprint 26 with no capacity
 * entered and nothing in it, and a Backlog whose first two items end Sprint 26 and Sprint 27 in the forecast.
 */
export function planningView(overrides: Partial<BacklogViewDto> = {}): BacklogViewDto {
  const backlog = [
    itemRow({ key: 'SHIP-170', rank: 1, points: 5 }),
    itemRow({ key: 'SHIP-171', rank: 2, points: 8 }),
    itemRow({ key: 'SHIP-172', rank: 3, points: 3 }),
  ];
  return {
    ...backlogView([]),
    mode: 'planning',
    totalCount: 6,
    sections: [
      {
        id: CURRENT.id,
        title: CURRENT.name,
        sprint: CURRENT,
        points: 26,
        capacity: 23,
        rows: [itemRow({ key: 'SHIP-160', rank: 1, sprint: CURRENT, points: 13 })],
      },
      {
        id: FUTURE.id,
        title: FUTURE.name,
        sprint: FUTURE,
        points: 8,
        capacity: 22,
        rows: [itemRow({ key: 'SHIP-158', rank: 1, sprint: FUTURE, points: 8 })],
      },
      { id: LATER.id, title: LATER.name, sprint: LATER, points: 0, capacity: null, rows: [] },
      { id: 'backlog', title: 'Backlog', sprint: null, points: 16, capacity: null, rows: backlog },
    ],
    forecast: {
      sprints: [
        {
          sprintId: LATER.id,
          name: 'Sprint 26',
          room: 21,
          fromCapacity: false,
          lastItemId: backlog[1].id,
          itemCount: 2,
          points: 13,
          unestimatedCount: 0,
        },
        {
          sprintId: null,
          name: 'Sprint 27',
          room: 21,
          fromCapacity: false,
          lastItemId: backlog[2].id,
          itemCount: 1,
          points: 3,
          unestimatedCount: 0,
        },
      ],
    },
    velocity: {
      window: 3,
      average: 21,
      override: null,
      sprints: [20, 22, 21].map((completed, i) => ({
        sprintId: `old${i}`,
        sprintName: `Sprint ${21 + i}`,
        committed: completed,
        completed,
      })),
    },
    ...overrides,
  };
}

const EPIC = { id: 'epic', name: 'Epic' } as const;
const FEATURE = { id: 'feature', name: 'Feature' } as const;
const ACTIVE = { id: 's-active', name: 'Active', category: 'inProgress' } as const;

/** An Epic or Feature group row; `depth` is its nesting, `childPoints` its open points. */
export function groupRow(
  overrides: Partial<BacklogRowDto> & { key: string; depth: number },
): BacklogRowDto {
  return itemRow({
    kind: 'group',
    type: overrides.depth === 0 ? EPIC : FEATURE,
    state: ACTIVE,
    points: null,
    rank: null,
    childPoints: 0,
    rollups: [{ type: 'story', open: 0, closed: 0 }],
    ...overrides,
  });
}

/** The No parent group row, last in a tree. */
export function noParentRow(overrides: Partial<BacklogRowDto> = {}): BacklogRowDto {
  return itemRow({
    kind: 'noParentGroup',
    key: '',
    id: 'no-parent',
    type: null,
    state: null,
    title: 'No parent',
    depth: 0,
    points: null,
    rank: null,
    childPoints: 0,
    ...overrides,
  });
}

const parentOf = (group: BacklogRowDto): BacklogRowDto['parent'] => ({
  id: group.id,
  key: group.key,
  type: group.type ?? FEATURE,
  title: group.title,
});

/**
 * A tree: Epic SHIP-90 > Feature SHIP-101 (stories SHIP-141, SHIP-144; nested Feature SHIP-110 with SHIP-150), Feature
 * SHIP-102 (SHIP-129), then No parent (SHIP-170). SHIP-160's parent is a Removed Feature, so it is placed under Epic SHIP-90, among the Epic's own stories, which come before its Features.
 */
export function treeRows() {
  const epic = groupRow({ key: 'SHIP-90', depth: 0, title: 'Storefront' });
  const checkout = groupRow({
    key: 'SHIP-101',
    depth: 1,
    title: 'Checkout v2',
    childPoints: 11,
    rollups: [{ type: 'story', open: 3, closed: 1 }],
    parent: parentOf(epic),
  });
  const nested = groupRow({
    key: 'SHIP-110',
    depth: 2,
    title: 'Receipts',
    parent: parentOf(checkout),
  });
  const mobile = groupRow({
    key: 'SHIP-102',
    depth: 1,
    title: 'Mobile sign-in',
    childPoints: null,
    sumWarn: true,
    rollups: [{ type: 'story', open: 1, closed: 0 }],
    parent: parentOf(epic),
  });
  const removed = { id: 'removed', key: 'SHIP-99', type: FEATURE, title: 'Old checkout' };
  const rows = [
    epic,
    itemRow({ key: 'SHIP-160', rank: 5, depth: 1, parent: removed, revision: 7 }),
    checkout,
    itemRow({ key: 'SHIP-141', rank: 1, depth: 2, parent: parentOf(checkout), revision: 4 }),
    itemRow({ key: 'SHIP-144', rank: 2, depth: 2, parent: parentOf(checkout) }),
    nested,
    itemRow({ key: 'SHIP-150', rank: 3, depth: 3, parent: parentOf(nested) }),
    mobile,
    itemRow({ key: 'SHIP-129', rank: 4, depth: 2, parent: parentOf(mobile), revision: 6 }),
    noParentRow({ rollups: [{ type: 'story', open: 1, closed: 0 }], childPoints: 3 }),
    itemRow({ key: 'SHIP-170', rank: 6, depth: 1, parent: null, revision: 8 }),
  ];
  return { rows, epic, checkout, nested, mobile };
}

export function treeView(rows: BacklogRowDto[]): BacklogViewDto {
  return { ...backlogView(rows), mode: 'tree' };
}

/** The project's sprints as the shared sprints read answers them, for the planning view's three sprints. */
export function planningSprints(): SprintDto[] {
  const sprint = (
    ref: { id: string; name: string; status: SprintDto['status'] },
    datesLabel: string,
  ) => ({
    ...ref,
    startDate: '2026-10-20',
    endDate: '2026-11-02',
    goal: '',
    rolloverStatus: null,
    datesLabel,
    timeLabel: '',
  });
  return [
    sprint(CURRENT, '20 Oct – 2 Nov'),
    sprint(FUTURE, '3 Nov – 16 Nov'),
    sprint(LATER, '17 Nov – 30 Nov'),
  ];
}
