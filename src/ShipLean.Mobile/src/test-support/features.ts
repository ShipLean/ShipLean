// Pure fake data and fake routes for the Features screens (random UUIDs). The rows follow design/mobileapp/feature-board.html.
import type { BulkWriteResult } from '@shiplean/core/api/backlogWrites';
import type { CommentDto } from '@shiplean/core/api/comments';
import type {
  DigestItemDto,
  DigestLineDto,
  DigestLineKind,
  FeatureDigestDto,
  FeatureGroupDto,
  FeatureRowDto,
  FeaturesOverviewDto,
} from '@shiplean/core/api/featuresOverview';
import type { StateDto } from '@shiplean/core/api/projects';
import type { StateCategory, StateRef } from '@shiplean/core/api/refs';
import type { FeatureBoardViewDto } from '@shiplean/core/api/views';
import commentFixtures from '../../../ShipLean.TestSupport/DtoFixtures/comments/comments.json';
import states from '../../../ShipLean.TestSupport/DtoFixtures/projects/states.json';
import featureBoard from '../../../ShipLean.TestSupport/DtoFixtures/views/feature-board-view.json';
import { aProject, aWriteResult } from './builders';
import { controlledApi, type ControlledApi } from './controlledApi';
import type { ApiTable, FakeRequest } from './fakeApi';
import { signedInApi } from './signedInApi';

const uuid = (): string => globalThis.crypto.randomUUID();

const STATES: Record<StateCategory, string> = {
  proposed: 'New',
  inProgress: 'Active',
  resolved: 'Resolved',
  completed: 'Closed',
  removed: 'Removed',
};

const stateRef = (category: StateCategory): StateRef => ({
  id: uuid(),
  name: STATES[category],
  category,
});

export function aFeatureRow(overrides: Partial<FeatureRowDto> = {}): FeatureRowDto {
  return {
    id: uuid(),
    key: 'SHIP-1',
    title: 'A Feature',
    revision: 1,
    state: stateRef('inProgress'),
    health: null,
    statusLine: null,
    statusVerified: null,
    statusStale: false,
    rollups: [],
    needsYou: null,
    rank: 1,
    epic: null,
    ...overrides,
  };
}

// A group of the overview. Closed is folded: it carries no rows, only its count.
export function aFeatureGroup(
  stateCategory: StateCategory,
  rows: FeatureRowDto[] = [],
  overrides: Partial<FeatureGroupDto> = {},
): FeatureGroupDto {
  const folded = stateCategory === 'completed';
  return {
    stateCategory,
    label: STATES[stateCategory],
    folded,
    count: rows.length,
    rows: folded ? [] : rows,
    ...overrides,
  };
}

export function anOverview(
  groups: FeatureGroupDto[],
  overrides: Partial<FeaturesOverviewDto> = {},
): FeaturesOverviewDto {
  return {
    projectId: aProject().id,
    sinceLabel: 'since Mon 09:12',
    changedSince: 0,
    groups,
    ...overrides,
  };
}

const taskRollup = (open: number, closed: number) => [
  { type: 'story' as const, open, closed, inCurrentSprint: open, inOtherSprint: 0 },
];

const verified = (atLabel: string, stale: boolean, by: string) => ({
  at: '2030-01-01T09:00:00+00:00',
  atLabel,
  stale,
  by: {
    id: uuid(),
    name: by,
    initials: by.slice(0, 2).toUpperCase(),
    avatarColour: '#5e81ac',
    photoUrl: null,
  },
  agent: null,
  via: null,
});

// The design's board: Active (one at risk and waiting, one blocked and stale, one on track), Resolved, New and Closed
// folded; the Epic is set on two Features.
export function designOverview(): FeaturesOverviewDto {
  const epic = {
    id: uuid(),
    key: 'SHIP-700',
    type: { id: 'epic' as const, name: 'Epic' },
    title: 'Phone and mobile web',
  };
  const active = [
    aFeatureRow({
      key: 'SHIP-906',
      title: 'Voice commands with on-device models',
      health: 'atRisk',
      statusLine:
        'Parakeet runs at 1.4x real time on the S22; the 4 GB phone fallback is still open.',
      statusVerified: verified('3h ago', false, 'Claude Code'),
      rollups: taskRollup(7, 2),
      needsYou: { questions: 1, decisions: 0, onChildren: true },
      rank: 1,
      epic,
    }),
    aFeatureRow({
      key: 'SHIP-743',
      title: 'Nightly backups',
      health: 'blocked',
      statusLine: 'Blocked: the host has no pg_dump 18 and no Docker.',
      statusVerified: verified('4 days old', true, 'Sam'),
      statusStale: true,
      rollups: taskRollup(6, 1),
      needsYou: { questions: 0, decisions: 1, onChildren: false },
      rank: 2,
    }),
    aFeatureRow({
      key: 'SHIP-707',
      title: 'Mobile web',
      health: 'onTrack',
      statusLine: 'Retro and insights screens built; touch drag is in review.',
      statusVerified: verified('yesterday', false, 'Priya'),
      rollups: taskRollup(6, 18),
      rank: 3,
      epic,
    }),
  ];
  const resolved = [
    aFeatureRow({
      key: 'SHIP-893',
      title: 'Progress bar on sprints',
      state: stateRef('resolved'),
      health: 'onTrack',
      statusLine: 'Built; dark-mode colours wait for approval.',
      statusVerified: verified('2d ago', false, 'Claude Code'),
      rollups: taskRollup(0, 4),
      needsYou: { questions: 0, decisions: 1, onChildren: false },
      rank: 4,
    }),
  ];
  const proposed = [
    aFeatureRow({
      key: 'SHIP-892',
      title: 'Demo artifacts for the sprint demo agent',
      state: stateRef('proposed'),
      rank: 5,
    }),
  ];
  return anOverview(
    [
      aFeatureGroup('inProgress', active),
      aFeatureGroup('resolved', resolved),
      aFeatureGroup('proposed', proposed),
      aFeatureGroup('completed', [], { count: 2 }),
    ],
    { changedSince: 3 },
  );
}

export function aDigestLine(
  kind: DigestLineKind,
  overrides: Partial<DigestLineDto> = {},
): DigestLineDto {
  const texts: Record<DigestLineKind, string> = {
    state: 'New to Active',
    health: 'Health set to At risk',
    statusLine: 'Status line updated',
    comment: 'Added a note',
    agentWork: 'An agent picked it up',
    ask: 'Asked a question',
  };
  return {
    kind,
    text: texts[kind],
    by: null,
    agentName: null,
    at: '2030-01-01T09:00:00+00:00',
    atLabel: 'Sat 21:05',
    ...overrides,
  };
}

export function aDigestItem(overrides: Partial<DigestItemDto> = {}): DigestItemDto {
  return {
    id: uuid(),
    key: 'SHIP-907',
    type: 'feature',
    title: 'Mobile app foundations',
    lines: [aDigestLine('state')],
    ...overrides,
  };
}

export function aDigest(overrides: Partial<FeatureDigestDto> = {}): FeatureDigestDto {
  return {
    since: '2030-01-01T09:12:00+00:00',
    sinceLabel: 'Mon 09:12',
    needsYou: [
      aDigestItem({
        key: 'SHIP-906',
        title: 'Voice commands with on-device models',
        lines: [aDigestLine('ask')],
      }),
    ],
    changed: [
      aDigestItem({ lines: [aDigestLine('state'), aDigestLine('health')] }),
      aDigestItem({
        key: 'SHIP-910',
        type: 'story',
        title: 'Pick the speech model per RAM tier',
        lines: [aDigestLine('agentWork')],
      }),
    ],
    cursor: null,
    more: false,
    ...overrides,
  };
}

// SHIP-914's board: the fixture's rows, headed by SHIP-906, with the first story waiting on you.
export function designBoard(): FeatureBoardViewDto {
  const board = structuredClone(featureBoard) as FeatureBoardViewDto;
  board.header = {
    ...board.header,
    key: 'SHIP-906',
    title: 'Voice commands with on-device models',
  };
  board.rows = board.rows.map((row, i) => ({
    ...row,
    needsYou: i === 0 ? { questions: 1, decisions: 0, onChildren: false } : null,
  }));
  return board;
}

export interface FeaturesApiOptions {
  projectId?: string;
  overview?: FeaturesOverviewDto;
  digest?: FeatureDigestDto;
  board?: FeatureBoardViewDto;
  // The Feature keys the by-key board answers for.
  boardKeys?: string[];
}

// The id every comment POST answers with, so the archive route (the Undo of a note) is a known path.
export const NOTE_ID = '00000008-0000-4000-8001-0000000000f1';

function aComment(itemId: string, body: string): CommentDto {
  return {
    ...(commentFixtures.comments[0] as CommentDto),
    id: NOTE_ID,
    workItemId: itemId,
    body,
    bodyHtml: `<p>${body}</p>\n`,
  };
}

// Answers the overview, digest, seen, the by-key Feature board, the project states, PATCH, bulk placement, the asks
// writes, the status line and comments. A route in `table` replaces the default. It fails, goes offline and hangs on
// demand because it is a controlledApi.
export function featuresApi(table: ApiTable = {}, opts: FeaturesApiOptions = {}): ControlledApi {
  const projectId = opts.projectId ?? aProject().id;
  const overview = opts.overview ?? designOverview();
  const board = opts.board ?? designBoard();
  const project = `/api/projects/${projectId}`;
  const rows = overview.groups.flatMap((g) => g.rows);
  const routes: ApiTable = {
    ...signedInApi(),
    [`GET ${project}/features/overview`]: overview,
    [`GET ${project}/features/digest`]: opts.digest ?? aDigest(),
    [`POST ${project}/features/digest/seen`]: { status: 204 },
    [`GET ${project}/states`]: (states as StateDto[]).filter((state) => state.type === 'feature'),
    [`POST ${project}/backlog/bulk`]: (request: FakeRequest): BulkWriteResult => ({
      written: (request.body as { items: { id: string }[] }).items.map((i) =>
        aWriteResult({ id: i.id }),
      ),
      stale: [],
    }),
    [`POST /api/comments/${NOTE_ID}/archive`]: { status: 204 },
  };
  for (const key of opts.boardKeys ?? [board.header.key, ...rows.map((r) => r.key)]) {
    routes[`GET ${project}/work-items/by-key/${key}/feature-board`] = board;
  }
  for (const row of rows) {
    const item = `/api/work-items/${row.id}`;
    routes[`PATCH ${item}`] = aWriteResult({ id: row.id, key: row.key });
    routes[`POST ${item}/decisions/confirm`] = { status: 204 };
    for (let number = 1; number <= 9; number++) {
      routes[`POST ${item}/questions/${number}/answer`] = { status: 204 };
    }
    routes[`POST ${item}/comments`] = (request: FakeRequest): CommentDto =>
      aComment(row.id, (request.body as { body: string }).body);
  }
  return controlledApi({ ...routes, ...table });
}
