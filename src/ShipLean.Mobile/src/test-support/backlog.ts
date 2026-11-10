// Pure fake data and fake routes for the Backlog (random UUIDs). The rows follow design/mobileapp/backlog.html.
import type { BacklogLevel } from '@shiplean/core/api/backlog';
import type { BacklogAddResult, BulkWriteResult } from '@shiplean/core/api/backlogWrites';
import type { CommentDto } from '@shiplean/core/api/comments';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { BacklogRowDto, BacklogViewDto, RollupCountDto } from '@shiplean/core/api/views';
import type { WriteResult } from '@shiplean/core/api/workItems';
import commentFixtures from '../../../ShipLean.TestSupport/DtoFixtures/comments/comments.json';
import { aWriteResult } from './builders';
import type { ApiTable, FakeRequest } from './fakeApi';

const PROJECT_ID = '00000002-0000-4000-8000-000000000001';

function uuid(): string {
  return 'xxxxxxxx-xxxx-4xxx-8xxx-xxxxxxxxxxxx'.replace(/x/g, () =>
    Math.floor(Math.random() * 16).toString(16),
  );
}

const TYPE_NAMES: Record<string, string> = {
  epic: 'Epic',
  feature: 'Feature',
  story: 'User Story',
};

export function aBacklogRow(overrides: Partial<BacklogRowDto> = {}): BacklogRowDto {
  const type = (overrides.type?.id ?? 'story') as WorkItemTypeId;
  return {
    kind: 'item',
    id: uuid(),
    key: 'SHIP-1',
    type: { id: type, name: TYPE_NAMES[type] ?? type },
    title: 'A backlog row',
    state: { id: uuid(), name: 'New', category: 'proposed' },
    assignee: null,
    points: null,
    childPoints: null,
    sumWarn: false,
    sprint: null,
    parent: null,
    depth: 0,
    rank: 1,
    tags: [],
    rollups: [],
    revision: 1,
    health: null,
    needsYou: null,
    blockedBy: [],
    ...overrides,
  };
}

// A list-mode view of one level; rows keep the order given.
export function aBacklogView(level: BacklogLevel, rows: BacklogRowDto[]): BacklogViewDto {
  return {
    projectId: PROJECT_ID,
    level,
    mode: 'list',
    totalCount: rows.length,
    rows,
    sections: [],
    tagColours: [],
    forecast: null,
    velocity: null,
    triageCount: 0,
    triageStateIds: [],
    review: null,
  };
}

function ranked(rows: Omit<Partial<BacklogRowDto>, 'rank'>[]): BacklogRowDto[] {
  return rows.map((row, i) => aBacklogRow({ ...row, rank: i + 1 }));
}

const feature = (key: string, title: string): Partial<BacklogRowDto> => ({
  key,
  title,
  type: { id: 'feature', name: 'Feature' },
});

// The Features level of the design: 7 Features in rank order, 3 waiting on you, SHIP-906 blocked by SHIP-907.
export function designFeatures(): BacklogRowDto[] {
  return ranked([
    { ...feature('SHIP-707', 'Mobile web: touch shell and phone layouts'), health: 'onTrack' },
    {
      ...feature('SHIP-907', 'Mobile app foundations'),
      health: 'atRisk',
      needsYou: { questions: 0, decisions: 2, onChildren: false },
    },
    { ...feature('SHIP-892', 'Demo artifacts for the sprint demo agent') },
    {
      ...feature('SHIP-906', 'Voice commands on the mobile app with on-device models'),
      health: 'atRisk',
      blockedBy: ['SHIP-907'],
      needsYou: { questions: 1, decisions: 0, onChildren: true },
    },
    { ...feature('SHIP-891', 'Features panel on the sprint page') },
    {
      ...feature('SHIP-246', 'Client notes on the work item page'),
      needsYou: { questions: 1, decisions: 1, onChildren: false },
    },
    { ...feature('SHIP-256', 'Measure web vitals on every page') },
  ]);
}

const taskRollup = (open: number, closed: number): RollupCountDto[] => [
  { type: 'task', open, closed, inCurrentSprint: open, inOtherSprint: 0 },
];

// The Stories level of the design, with each story's task rollups.
export function designStories(): BacklogRowDto[] {
  const story = (key: string, title: string, feature: string): Partial<BacklogRowDto> => ({
    key,
    title,
    type: { id: 'story', name: 'User Story' },
    parent: {
      id: uuid(),
      key: feature,
      type: { id: 'feature', name: 'Feature' },
      title: feature,
    },
  });
  return ranked([
    {
      ...story('SHIP-921', 'Download the speech and command models on first run', 'SHIP-906'),
      rollups: taskRollup(4, 3),
    },
    {
      ...story('SHIP-923', 'Hold-to-talk button and listening sheet', 'SHIP-906'),
      rollups: taskRollup(1, 2),
    },
    {
      ...story('SHIP-911', 'Development build with the native audio and model modules', 'SHIP-907'),
      rollups: taskRollup(3, 1),
    },
    { ...story('SHIP-912', 'Sign in on the phone through the browser', 'SHIP-907') },
    { ...story('SHIP-924', 'Note action copies words from the transcript', 'SHIP-906') },
    { ...story('SHIP-736', 'Sign-in screen on mobile web', 'SHIP-707') },
    { ...story('SHIP-925', 'Preview, apply and undo for voice actions', 'SHIP-906') },
  ]);
}

export interface BacklogRoutes {
  epics: BacklogRowDto[];
  features: BacklogRowDto[];
  stories: BacklogRowDto[];
}

export interface BacklogApiOptions {
  projectId?: string;
  rows?: Partial<BacklogRoutes>;
}

// The id every comment POST answers with, so the archive route (the Undo of a note) is a known path.
export const NOTE_ID = '00000008-0000-4000-8001-0000000000a1';

function aComment(itemId: string, body: string): CommentDto {
  return {
    ...(commentFixtures.comments[0] as CommentDto),
    id: NOTE_ID,
    workItemId: itemId,
    body,
    bodyHtml: `<p>${body}</p>\n`,
  };
}

// Answers the three level reads, reorder, bulk placement, add, the asks confirm, comments and the work-item PATCH.
// A route in `table` replaces the default. Wrap it in controlledApi to fail, go offline or hang a route.
export function backlogApi(table: ApiTable = {}, opts: BacklogApiOptions = {}): ApiTable {
  const projectId = opts.projectId ?? PROJECT_ID;
  const levels: BacklogRoutes = {
    epics: opts.rows?.epics ?? [],
    features: opts.rows?.features ?? designFeatures(),
    stories: opts.rows?.stories ?? designStories(),
  };
  const written = (id: string, key = 'SHIP-1'): WriteResult => aWriteResult({ id, key });
  const project = `/api/projects/${projectId}`;
  const routes: ApiTable = {
    [`GET ${project}/backlogs/epics`]: aBacklogView('epics', levels.epics),
    [`GET ${project}/backlogs/features`]: aBacklogView('features', levels.features),
    [`GET ${project}/backlog`]: aBacklogView('stories', levels.stories),
    [`POST ${project}/backlog/bulk`]: (request: FakeRequest): BulkWriteResult => ({
      written: (request.body as { items: { id: string }[] }).items.map((i) => written(i.id)),
      stale: [],
    }),
    [`POST ${project}/backlog/items`]: (request: FakeRequest): BacklogAddResult => ({
      created: (request.body as { items: unknown[] }).items.map(() => written(uuid())),
    }),
    [`POST /api/comments/${NOTE_ID}/archive`]: { status: 204 },
  };
  for (const row of [...levels.epics, ...levels.features, ...levels.stories]) {
    const item = `/api/work-items/${row.id}`;
    routes[`POST ${item}/reorder`] = written(row.id, row.key);
    routes[`PATCH ${item}`] = written(row.id, row.key);
    routes[`POST ${item}/decisions/confirm`] = { status: 204 };
    routes[`POST ${item}/comments`] = (request: FakeRequest): CommentDto =>
      aComment(row.id, (request.body as { body: string }).body);
  }
  return { ...routes, ...table };
}
