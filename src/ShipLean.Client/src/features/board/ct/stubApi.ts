import type { BoardViewDto } from '../../../api/views';
import type { UserRef } from '../../../api/refs';
import type { MemberDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import type { ChildSummaryDto, WorkItemCreate, WriteResult } from '../../../api/workItems';
import type { BoardMoveRequest } from '../boardApi';
import { BOARD_ID } from './fixtures';

// The part of Playwright's Page and Route these stubs use (the CT package does not re-export the types).
interface StubRoute {
  fulfill(response: { status: number; contentType?: string; body?: string }): Promise<void>;
  request(): { method(): string; url(): string; postDataJSON(): unknown };
}
interface Page {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

export interface BoardServer {
  /** The query strings of the board reads, in order. */
  reads: string[];
  /** The bodies of the work items created. */
  creates: WorkItemCreate[];
  /** Replaces what the next reads answer (a teammate changed the board). */
  setBoard: (view: BoardViewDto) => void;
  /** The bodies of the moves received, in order (held ones included). */
  moves: BoardMoveRequest[];
  /** Answers the moves held by `holdMoves`. */
  release: () => void;
  /** Moves of this item wait until the returned function is called (the others are answered at once). */
  holdMovesOf: (itemId: string) => () => void;
  /** Board reads that arrive from now on wait for the returned function (a slow answer on its way). */
  pauseReads: () => () => void;
  /** The saved preferences by key (start with `options.preferences`); a PUT replaces the value. */
  preferences: Map<string, unknown>;
  /** The preference writes received, in order. */
  preferenceWrites: { key: string; value: unknown }[];
  /** The `type` of each children read, with the item it asked about ("id-SHIP-101 story"). */
  childReads: string[];
}

/** How the stub answers a move: a status and body, or the default (the revision after the one sent). */
export type MoveAnswer = { status: number; body: unknown } | undefined;

/** Stubs what the board makes: the boards list, the board read (the view may depend on `older`) and the create. */
export async function stubBoard(
  page: Page,
  view: BoardViewDto | ((search: URLSearchParams) => BoardViewDto),
  options: {
    createStatus?: number;
    createBody?: unknown;
    level?: 'stories' | 'features' | 'epics';
    /** Moves wait until `release()`. */
    holdMoves?: boolean;
    /** Answers a move; undefined = accepted. */
    answerMove?: (body: BoardMoveRequest) => MoveAnswer;
    /** The write result of an accepted move. */
    result?: (body: BoardMoveRequest) => Partial<WriteResult>;
    /** Refuses a move whose revision is not the item's current one (starts at 1, +1 per accepted move). */
    strictRevisions?: boolean;
    /** Preferences already saved, by key (a lane choice, Full / Compact). */
    preferences?: Record<string, unknown>;
    /** The project's members, sprints and Epics, for the filter bar. */
    members?: UserRef[];
    sprints?: Pick<SprintDto, 'id' | 'name'>[];
    epics?: { id: string; title: string }[];
    /** The children of a card for the roll-up's type; none by default. */
    children?: (itemId: string, type: string) => ChildSummaryDto[];
  } = {},
): Promise<BoardServer> {
  let current = view;
  const server: BoardServer = {
    reads: [],
    creates: [],
    moves: [],
    preferences: new Map(Object.entries(options.preferences ?? {})),
    preferenceWrites: [],
    childReads: [],
    release: () => undefined,
    holdMovesOf: (itemId) => {
      let open: () => void = () => undefined;
      gates.set(
        itemId,
        new Promise<void>((resolve) => {
          open = resolve;
        }),
      );
      return open;
    },
    pauseReads: () => {
      let resume: () => void = () => undefined;
      readGate = new Promise<void>((resolve) => {
        resume = resolve;
      });
      return resume;
    },
    setBoard: (next) => {
      current = next;
    },
  };
  const revisions = new Map<string, number>();
  const gates = new Map<string, Promise<void>>();
  let readGate: Promise<void> | null = null;
  let release: () => void = () => undefined;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.release = release;
  await page.route('**/api/me/preferences/*', async (route) => {
    const request = route.request();
    const key = decodeURIComponent(request.url().split('/').pop() ?? '');
    if (request.method() === 'PUT') {
      const value = request.postDataJSON() as unknown;
      server.preferenceWrites.push({ key, value });
      server.preferences.set(key, value);
      return route.fulfill({ status: 204 });
    }
    return server.preferences.has(key)
      ? route.fulfill(json({ key, json: server.preferences.get(key) }))
      : route.fulfill({ status: 204 });
  });
  await page.route('**/api/projects/p1/members', (route) =>
    route.fulfill(
      json(
        (options.members ?? []).map(
          (user) =>
            ({
              user,
              email: `${user.id}@example.test`,
              role: 'member',
              status: 'active',
            }) satisfies MemberDto,
        ),
      ),
    ),
  );
  await page.route('**/api/projects/p1/sprints', (route) =>
    route.fulfill(json(options.sprints ?? [])),
  );
  await page.route('**/api/projects/p1/work-items?*', (route) =>
    route.fulfill(json((options.epics ?? []).map((epic) => ({ ...epic, type: { id: 'epic' } })))),
  );
  await page.route('**/api/work-items/*/children*', (route) => {
    const url = new URL(route.request().url());
    const itemId = url.pathname.split('/').at(-2) ?? '';
    const type = url.searchParams.get('type') ?? '';
    server.childReads.push(`${itemId} ${type}`);
    return route.fulfill(json(options.children?.(itemId, type) ?? []));
  });
  await page.route('**/api/projects/p1/boards', (route) =>
    route.fulfill(json([{ id: BOARD_ID, level: options.level ?? 'stories', name: 'Board' }])),
  );
  await page.route(`**/api/boards/${BOARD_ID}/moves`, async (route) => {
    const body = route.request().postDataJSON() as BoardMoveRequest;
    server.moves.push(body);
    if (options.holdMoves) await held;
    await gates.get(body.itemId);
    const answer = options.answerMove?.(body);
    if (answer) return route.fulfill(json(answer.body, answer.status));
    const current = revisions.get(body.itemId) ?? 1;
    if (options.strictRevisions && body.expectedRevision !== current) {
      return route.fulfill(
        json(
          { error: 'Changed by someone else.', code: 'conflict', current: {}, theirChanges: [] },
          409,
        ),
      );
    }
    revisions.set(body.itemId, current + 1);
    return route.fulfill(
      json({
        id: body.itemId,
        key: body.itemId.replace('id-', ''),
        revision: current + 1,
        changes: [],
        notices: [],
        ...options.result?.(body),
      } satisfies WriteResult),
    );
  });
  await page.route(`**/api/boards/${BOARD_ID}*`, async (route) => {
    const search = new URL(route.request().url()).search;
    server.reads.push(search);
    const params = new URLSearchParams(search);
    const answer = typeof current === 'function' ? current(params) : current;
    await readGate;
    // A read the page gave up on (a move cancelled it) has nobody to answer.
    await route.fulfill(json(answer)).catch(() => undefined);
  });
  await page.route('**/api/work-items', (route) => {
    server.creates.push(route.request().postDataJSON() as WorkItemCreate);
    return route.fulfill(
      json(
        options.createBody ?? { id: 'new', key: 'SHIP-184', revision: 1, changes: [], notices: [] },
        options.createStatus ?? 200,
      ),
    );
  });
  return server;
}
