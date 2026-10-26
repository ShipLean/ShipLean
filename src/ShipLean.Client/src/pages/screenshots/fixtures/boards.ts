import type { BoardSettingsDto } from '../../../api/boards';
import type { BoardColumnJson } from '../../../api/persisted';
import type { UserRef } from '../../../api/refs';
import type {
  BoardCellDto,
  BoardColumnDto,
  BoardLaneDto,
  BoardViewDto,
  CardDto,
} from '../../../api/views';
import type { ChildSummaryDto } from '../../../api/workItems';
import { card, cell, column, lane, SETTINGS, BOARD_ID } from '../../../features/board/ct/fixtures';
import {
  emptySettings,
  boardSettingsDto,
} from '../../../features/boardSettings/ct/boardSettingsFixtures';
import { styleRule } from '../../../features/boardSettings/ct/stubBoard';
import { PEOPLE } from '../shellStub';

// The design's Storefront sample (design/screens/features-board.html, board-settings.html) as board read models.
export { BOARD_ID };

const NEW = { id: 's-new', name: 'New', category: 'proposed' } as const;
const ACTIVE = { id: 's-act', name: 'Active', category: 'inProgress' } as const;
const RESOLVED = { id: 's-res', name: 'Resolved', category: 'resolved' } as const;
const CLOSED = { id: 's-closed', name: 'Closed', category: 'completed' } as const;

const FEATURE = { id: 'feature', name: 'Feature' } as const;
const EPIC = { id: 'epic', name: 'Epic' } as const;
const STORY = { id: 'story', name: 'User Story' } as const;
const BUG = { id: 'bug', name: 'Bug' } as const;

const ref = (u: UserRef) => u;
const tag = (name: string, colour: 'ok' | 'danger' | 'info' | 'muted' | 'warn' | null) => ({
  name,
  colour,
});
const roll = (type: 'story' | 'bug' | 'feature', closed: number, total: number) => ({
  type,
  open: total - closed,
  closed,
});

const epicRef = (n: number, title: string) => ({
  id: `id-SHIP-${n}`,
  key: `SHIP-${n}`,
  type: EPIC,
  title,
});
const CHECKOUT = epicRef(90, 'Checkout conversion');
const SECURITY = epicRef(91, 'Account security');
const INSIGHTS = epicRef(92, 'Insights for merchants');
const PLATFORM = epicRef(93, 'Platform health');
const RESILIENCE = epicRef(89, 'Payments resilience');

const feature = (
  n: number,
  title: string,
  parent: ReturnType<typeof epicRef>,
  points: number | null,
  childPoints: number | null,
  extra: Partial<CardDto> = {},
): CardDto =>
  card({
    key: `SHIP-${n}`,
    type: FEATURE,
    title,
    parent,
    points,
    childPoints,
    ...extra,
  });

const featureColumns = (): BoardColumnDto[] => [
  column({ id: 'col-new', name: 'New', count: 2, states: [{ type: 'feature', state: NEW }] }),
  column({
    id: 'col-shaping',
    name: 'Shaping',
    count: 3,
    states: [{ type: 'feature', state: NEW }],
  }),
  column({
    id: 'col-prog',
    name: 'In progress',
    wipLimit: 3,
    count: 3,
    states: [{ type: 'feature', state: ACTIVE }],
  }),
  column({
    id: 'col-rel',
    name: 'Releasing',
    wipLimit: 2,
    count: 1,
    states: [{ type: 'feature', state: RESOLVED }],
  }),
  column({ id: 'col-done', name: 'Done', count: 4, states: [{ type: 'feature', state: CLOSED }] }),
];

const COLUMN_IDS = ['col-new', 'col-shaping', 'col-prog', 'col-rel', 'col-done'];

// One cell per column (none of these columns are split), cards by column id.
const cells = (byColumn: Record<string, CardDto[]>): BoardCellDto[] =>
  COLUMN_IDS.map((id) => cell(id, byColumn[id] ?? []));

const FEATURE_CARDS = {
  s106: feature(106, 'Order tracking page', CHECKOUT, null, null, { rollups: [] }),
  s109: feature(109, 'Merchant CSV exports', INSIGHTS, 5, 5, {
    rollups: [roll('story', 0, 2)],
    tags: [tag('Feedback Ticket', 'warn')],
  }),
  s107: feature(107, 'Two-factor recovery codes', SECURITY, 8, 8, {
    rollups: [roll('story', 0, 3)],
    tags: [tag('Security', 'ok'), tag('Blocked', 'danger')],
    styleRuleId: 'rule-blocked',
  }),
  s108: feature(108, 'Dashboard widgets', INSIGHTS, 8, 13, { rollups: [roll('story', 0, 2)] }),
  s104: feature(104, 'Ops hygiene', PLATFORM, 5, 11, {
    rollups: [roll('story', 1, 3)],
    assignee: ref(PEOPLE.tom),
    tags: [tag('Tech debt', 'muted')],
  }),
  s101: feature(101, 'Checkout v2', CHECKOUT, 20, 26, {
    rollups: [roll('story', 3, 6), roll('bug', 1, 2)],
    assignee: ref(PEOPLE.priya),
    tags: [tag('Payments', 'ok')],
    sumWarn: true,
  }),
  s102: feature(102, 'Mobile sign-in', SECURITY, 21, 32, {
    rollups: [roll('story', 1, 4), roll('bug', 0, 1)],
    assignee: ref(PEOPLE.sam),
    tags: [tag('Mobile', 'info')],
    sumWarn: true,
  }),
  s103: feature(103, 'Reporting', INSIGHTS, 13, 28, {
    rollups: [roll('story', 0, 4), roll('bug', 1, 1), roll('feature', 0, 1)],
    assignee: ref(PEOPLE.ana),
    sumWarn: true,
  }),
  s111: feature(111, 'Login audit trail', SECURITY, 8, 9, {
    rollups: [roll('story', 3, 3), roll('bug', 1, 1)],
    assignee: ref(PEOPLE.mei),
    tags: [tag('Security', 'ok')],
    offerDone: true,
  }),
  s112: feature(112, 'Retry failed payments', RESILIENCE, 8, 8, {
    rollups: [roll('story', 2, 2)],
    assignee: ref(PEOPLE.priya),
    tags: [tag('Payments', 'ok')],
  }),
  s105: feature(105, 'Coupons v2', CHECKOUT, 13, 11, {
    rollups: [roll('story', 4, 4), roll('bug', 1, 1)],
    assignee: ref(PEOPLE.tom),
    tags: [tag('Payments', 'ok')],
  }),
  s110: feature(110, 'Status page', PLATFORM, 5, 5, {
    rollups: [roll('story', 2, 2)],
    assignee: ref(PEOPLE.tom),
  }),
  s113: feature(113, 'Card account updater', RESILIENCE, 5, 5, {
    rollups: [roll('story', 1, 1), roll('bug', 1, 1)],
    assignee: ref(PEOPLE.mei),
    tags: [tag('Payments', 'ok')],
  }),
};

const BLOCKED_RULE = styleRule(
  'rule-blocked',
  'Blocked',
  { field: 'tags', op: 'contains', value: 'Blocked' },
  { colour: 'danger' },
);

const f = FEATURE_CARDS;

/**
 * The Features board with no lanes (one default lane). `confirm` puts SHIP-102 in Releasing, one Alt+→ from Done, the
 * move the Done confirm dialog interrupts.
 */
export function featuresBoardNoLanes(confirm = false): BoardViewDto {
  const releasing = confirm ? [f.s111, f.s102] : [f.s111];
  const inProgress = confirm ? [f.s101, f.s103] : [f.s101, f.s102, f.s103];
  return {
    boardId: BOARD_ID,
    projectId: 'p1',
    level: 'features',
    openCount: 9,
    openPoints: 88,
    columns: featureColumns(),
    lanes: [
      lane({
        id: 'lane-default',
        name: 'Default lane',
        kind: 'default',
        cardCount: 13,
        openPoints: 88,
        cells: cells({
          'col-new': [f.s106, f.s109],
          'col-shaping': [f.s107, f.s108, f.s104],
          'col-prog': inProgress,
          'col-rel': releasing,
          'col-done': [f.s112, f.s105, f.s110, f.s113],
        }),
      }),
    ],
    ...SETTINGS,
    styleRules: [BLOCKED_RULE],
    annotations: { tasks: true, stories: true, bugs: true, features: true },
  };
}

const epicLane = (
  epic: ReturnType<typeof epicRef>,
  cardCount: number,
  openPoints: number,
  byColumn: Record<string, CardDto[]>,
): BoardLaneDto =>
  lane({
    id: `lane-${epic.key}`,
    name: epic.title,
    kind: 'epic',
    epic,
    cardCount,
    openPoints,
    cells: cells(byColumn),
  });

/** The Features board with Lanes By Epic: three Epic lanes (the design's Checkout, Security, Insights). */
export function featuresBoardByEpic(): BoardViewDto {
  return {
    ...featuresBoardNoLanes(),
    lanes: [
      epicLane(CHECKOUT, 3, 20, {
        'col-new': [f.s106],
        'col-prog': [f.s101],
        'col-done': [f.s105],
      }),
      epicLane(SECURITY, 3, 37, {
        'col-shaping': [f.s107],
        'col-prog': [f.s102],
        'col-rel': [f.s111],
      }),
      epicLane(INSIGHTS, 3, 26, {
        'col-new': [f.s109],
        'col-shaping': [f.s108],
        'col-prog': [f.s103],
      }),
    ],
  };
}

const child = (
  n: number,
  type: typeof STORY | typeof BUG | typeof FEATURE,
  title: string,
  state: typeof NEW | typeof ACTIVE | typeof CLOSED,
): ChildSummaryDto => ({
  id: `id-SHIP-${n}`,
  key: `SHIP-${n}`,
  type,
  title,
  state,
  assignee: null,
  points: null,
});

/** The stories under SHIP-101 Checkout v2 (the open roll-up list). */
export const CHECKOUT_V2_STORIES: ChildSummaryDto[] = [
  child(121, STORY, 'Pay with saved card', ACTIVE),
  child(122, STORY, 'Address autocomplete', ACTIVE),
  child(123, STORY, 'Gift card balance check', NEW),
  child(124, STORY, 'Order confirmation email', CLOSED),
  child(125, STORY, 'Guest checkout', CLOSED),
  child(126, STORY, 'Apple Pay button', CLOSED),
];

/** The open children of SHIP-102 Mobile sign-in, as the Done confirm lists them. */
export const MOBILE_SIGN_IN_OPEN: ChildSummaryDto[] = [
  child(131, STORY, 'Sign in with passkey', ACTIVE),
  child(132, STORY, 'Remember device for 30 days', NEW),
  child(133, BUG, 'Crash when biometric prompt is cancelled', NEW),
  child(134, STORY, 'SSO via Okta', NEW),
];

/** The features under SHIP-90 Checkout conversion (the open roll-up list on the Epics board). */
export const CHECKOUT_FEATURES: ChildSummaryDto[] = [
  child(101, FEATURE, 'Checkout v2', ACTIVE),
  child(106, FEATURE, 'Order tracking page', NEW),
  child(105, FEATURE, 'Coupons v2', CLOSED),
];

const epicCard = (
  n: number,
  title: string,
  points: number | null,
  childPoints: number | null,
  rollups: CardDto['rollups'],
  assignee: UserRef | null,
  tags: CardDto['tags'] = [],
): CardDto =>
  card({ key: `SHIP-${n}`, type: EPIC, title, points, childPoints, rollups, assignee, tags });

/** The Epics board: four columns, SHIP-90 in progress. */
export function epicsBoard(): BoardViewDto {
  const ids = ['col-new', 'col-prog', 'col-roll', 'col-done'];
  const states = [NEW, ACTIVE, RESOLVED, CLOSED];
  const names = ['New', 'In progress', 'Rolling out', 'Done'];
  const counts = [3, 2, 0, 1];
  const byColumn: Record<string, CardDto[]> = {
    'col-new': [
      epicCard(
        92,
        'Insights for merchants',
        30,
        33,
        [roll('feature', 0, 3), roll('story', 0, 6), roll('bug', 1, 1)],
        ref(PEOPLE.ana),
      ),
      epicCard(
        93,
        'Platform health',
        10,
        16,
        [roll('feature', 1, 2), roll('story', 3, 5)],
        ref(PEOPLE.tom),
        [tag('Tech debt', 'muted')],
      ),
      epicCard(94, 'Marketplace launch', null, null, [], null),
    ],
    'col-prog': [
      epicCard(
        90,
        'Checkout conversion',
        40,
        37,
        [roll('feature', 1, 3), roll('story', 7, 10), roll('bug', 2, 3)],
        ref(PEOPLE.priya),
        [tag('Payments', 'ok')],
      ),
      epicCard(
        91,
        'Account security',
        30,
        49,
        [roll('feature', 0, 3), roll('story', 4, 10), roll('bug', 1, 2)],
        ref(PEOPLE.sam),
        [tag('Security', 'ok')],
      ),
    ],
    'col-done': [
      epicCard(
        89,
        'Payments resilience',
        15,
        13,
        [roll('feature', 2, 2), roll('story', 3, 3), roll('bug', 1, 1)],
        ref(PEOPLE.priya),
        [tag('Payments', 'ok')],
      ),
    ],
  };
  return {
    boardId: BOARD_ID,
    projectId: 'p1',
    level: 'epics',
    openCount: 5,
    openPoints: 110,
    columns: ids.map((id, i) =>
      column({
        id,
        name: names[i],
        count: counts[i],
        wipLimit: id === 'col-prog' ? 3 : null,
        states: [{ type: 'epic', state: states[i] }],
      }),
    ),
    lanes: [
      lane({
        id: 'lane-default',
        name: 'Default lane',
        kind: 'default',
        cardCount: 6,
        openPoints: 110,
        cells: ids.map((id) => cell(id, byColumn[id] ?? [])),
      }),
    ],
    ...SETTINGS,
    annotations: { tasks: true, stories: true, bugs: true, features: true },
  };
}

// ---- Stories board settings (design/screens/board-settings.html) ----

const S = {
  new: { id: 'st-new', name: 'New', category: 'proposed' },
  active: { id: 'st-active', name: 'Active', category: 'inProgress' },
  resolved: { id: 'st-resolved', name: 'Resolved', category: 'resolved' },
  closed: { id: 'st-closed', name: 'Closed', category: 'completed' },
} as const;
const B = {
  new: { id: 'bg-new', name: 'New', category: 'proposed' },
  triaged: { id: 'bg-triaged', name: 'Triaged (custom)', category: 'proposed' },
  active: { id: 'bg-active', name: 'Active', category: 'inProgress' },
  resolved: { id: 'bg-resolved', name: 'Resolved', category: 'resolved' },
  closed: { id: 'bg-closed', name: 'Closed', category: 'completed' },
} as const;

const settingsColumn = (
  id: string,
  name: string,
  story: string,
  bug: string,
  extra: Partial<BoardColumnJson> = {},
): BoardColumnJson => ({
  id,
  name,
  wipLimit: null,
  split: false,
  definitionOfDone: '',
  stateMappings: [
    { type: 'story', stateId: story },
    { type: 'bug', stateId: bug },
  ],
  ...extra,
});

/** The Stories board's settings: five columns, In progress with a definition of done, and three style rules. */
export function storiesSettings(): BoardSettingsDto {
  return boardSettingsDto({
    id: BOARD_ID,
    projectId: 'p1',
    states: [
      { type: 'story', states: Object.values(S) },
      { type: 'bug', states: Object.values(B) },
    ],
    settings: {
      ...emptySettings(),
      columns: [
        settingsColumn('c-new', 'New', S.new.id, B.new.id),
        settingsColumn('c-ready', 'Ready', S.new.id, B.triaged.id, { wipLimit: 8, split: true }),
        settingsColumn('c-prog', 'In progress', S.active.id, B.active.id, {
          wipLimit: 5,
          definitionOfDone:
            '- Code and tests pushed on a branch\n- Tasks exist for the remaining work\n- Draft PR open and linked',
        }),
        settingsColumn('c-review', 'In review', S.resolved.id, B.resolved.id, { wipLimit: 2 }),
        settingsColumn('c-done', 'Done', S.closed.id, B.closed.id),
      ],
      styleRules: [
        styleRule(
          'r-blocked',
          'Blocked',
          { field: 'tags', op: 'contains', value: 'Blocked' },
          { colour: 'danger' },
        ),
        styleRule(
          'r-spike',
          'Spike',
          { field: 'tags', op: 'contains', value: 'Spike' },
          { colour: 'info' },
        ),
        styleRule(
          'r-expedite',
          'Expedite',
          { field: 'priority', op: '=', value: 1 },
          { colour: null, bold: true },
        ),
      ],
    },
  });
}

/** The Stories board read behind the settings dialog: column ids match `storiesSettings`, counts feed the Cards column. */
export function storiesBoardForSettings(): BoardViewDto {
  const counts = [5, 7, 4, 3, 7];
  const ids = ['c-new', 'c-ready', 'c-prog', 'c-review', 'c-done'];
  const names = ['New', 'Ready', 'In progress', 'In review', 'Done'];
  return {
    boardId: BOARD_ID,
    projectId: 'p1',
    level: 'stories',
    openCount: 19,
    openPoints: 57,
    columns: ids.map((id, i) =>
      column({
        id,
        name: names[i],
        count: counts[i],
        wipLimit: [null, 8, 5, 2, null][i],
        overLimit: id === 'c-review',
      }),
    ),
    lanes: [
      lane({
        id: 'lane-default',
        name: 'Default lane',
        kind: 'default',
        cardCount: 4,
        cells: ids.map((id) =>
          cell(
            id,
            id === 'c-new'
              ? [card({ key: 'SHIP-129', title: 'Sign in with passkey', points: 8 })]
              : [],
          ),
        ),
      }),
    ],
    ...SETTINGS,
  };
}
