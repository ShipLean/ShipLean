import type { TaskboardViewDto } from '../../../api/views';
import type { TaskboardMoveRequest, TaskboardMoveResultDto } from '../taskboardApi';
import type { WorkItemCreate } from '../../../api/workItems';

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

export interface TaskboardServer {
  reads: number;
  creates: WorkItemCreate[];
  // Every value PUT to `view:taskboard`.
  puts: unknown[];
  // Every body PATCHed to a work item.
  patches: unknown[];
  // Every body POSTed to the Taskboard moves endpoint.
  moves: TaskboardMoveRequest[];
  // Reads answered so far (a held read is not counted until it is released).
  served: number;
  // Lets the gated read (options.gateRead) answer.
  release: () => void;
  // Lets the gated move (options.gateMove) answer.
  releaseMove: () => void;
  // Lets the gated preference read (options.gatePreference) answer.
  releasePreference: () => void;
}

// Stubs everything the Taskboard slot makes: the read, the `view:taskboard` preference, /api/me and the work item create.
export async function stubTaskboard(
  page: Page,
  view: TaskboardViewDto,
  options: {
    saved?: unknown;
    // How the moves endpoint answers (default: 200, nothing to offer).
    moveAnswer?: { status: number; body: unknown };
    // Every read after the first never answers, so the board shows what the page itself did to it.
    holdLaterReads?: boolean;
    // This read (1 = the first) answers only once `release()` is called; every read after it never answers.
    gateRead?: number;
    // Refuses a move naming a revision other than the card's current one (409), and bumps the revision on each move
    // the way the store does. The reads keep answering the view as given.
    checkRevisions?: boolean;
    // This move request (1 = the first) is not answered until `releaseMove()` is called.
    gateMove?: number;
    // A move of this task is always refused (409).
    refuseTask?: string;
    // How a work item PATCH answers (default: 200).
    patchAnswer?: { status: number; body: unknown };
    // The preference read is not answered until `releasePreference()` is called.
    gatePreference?: boolean;
    // The user /api/me answers (default: Priya).
    me?: object;
  } = {},
): Promise<TaskboardServer> {
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let releaseMove: () => void = () => undefined;
  const moveGate = new Promise<void>((resolve) => {
    releaseMove = resolve;
  });
  let releasePreference: () => void = () => undefined;
  const preferenceGate = new Promise<void>((resolve) => {
    releasePreference = resolve;
  });
  const server: TaskboardServer = {
    reads: 0,
    creates: [],
    puts: [],
    patches: [],
    moves: [],
    served: 0,
    release,
    releaseMove,
    releasePreference,
  };
  const revisions = new Map<string, number>();
  for (const row of view.rows)
    for (const cell of row.cells) for (const t of cell.tasks) revisions.set(t.id, t.revision);
  let saved = options.saved;
  await page.route('**/api/sprints/*/taskboard', async (route) => {
    server.reads += 1;
    const lastAnswered = options.gateRead ?? (options.holdLaterReads ? 1 : Infinity);
    if (server.reads > lastAnswered) return new Promise<void>(() => undefined);
    if (server.reads === options.gateRead) await gate;
    server.served += 1;
    return route.fulfill(json(view));
  });
  await page.route('**/api/sprints/*/taskboard/moves', async (route) => {
    const sent = route.request().postDataJSON() as TaskboardMoveRequest;
    server.moves.push(sent);
    if (server.moves.length === options.gateMove) await moveGate;
    if (options.refuseTask === sent.taskId) {
      return route.fulfill(
        json(
          { error: 'Changed by someone else.', code: 'conflict', current: {}, theirChanges: [] },
          409,
        ),
      );
    }
    if (options.checkRevisions) {
      const current = revisions.get(sent.taskId);
      if (current !== sent.expectedRevision) {
        return route.fulfill(
          json({ error: 'Changed by someone else.', code: 'conflict', current: {} }, 409),
        );
      }
      revisions.set(sent.taskId, current + 1);
      return route.fulfill(
        json({
          written: {
            id: sent.taskId,
            key: 'SHIP-1',
            revision: current + 1,
            changes: [],
            notices: [],
          },
          parentId: null,
          offerResolve: false,
        } satisfies TaskboardMoveResultDto),
      );
    }
    const answer = options.moveAnswer ?? {
      status: 200,
      body: {
        written: { id: 'id', key: 'SHIP-173', revision: 2, changes: [], notices: [] },
        parentId: null,
        offerResolve: false,
      } satisfies TaskboardMoveResultDto,
    };
    return route.fulfill(json(answer.body, answer.status));
  });
  await page.route('**/api/me/preferences/**', async (route) => {
    if (route.request().method() === 'PUT') {
      saved = route.request().postDataJSON();
      server.puts.push(saved);
      return route.fulfill({ status: 204 });
    }
    if (options.gatePreference) await preferenceGate;
    return saved === undefined
      ? route.fulfill({ status: 204 })
      : route.fulfill(json({ key: 'view:taskboard', json: saved }));
  });
  await page.route('**/api/me', (route) =>
    route.fulfill(
      json(
        options.me ?? {
          id: 'u-priya',
          name: 'Priya Shah',
          initials: 'PS',
          avatarColour: '#2a7ab5',
          photoUrl: null,
        },
      ),
    ),
  );
  await page.route('**/api/work-items/*', (route) => {
    server.patches.push(route.request().postDataJSON());
    if (options.patchAnswer)
      return route.fulfill(json(options.patchAnswer.body, options.patchAnswer.status));
    return route.fulfill(
      json({ id: 'id', key: 'SHIP-141', revision: 2, changes: [], notices: [] }),
    );
  });
  await page.route('**/api/work-items', (route) => {
    server.creates.push(route.request().postDataJSON() as WorkItemCreate);
    return route.fulfill(
      json({ id: 'new', key: 'SHIP-184', revision: 1, changes: [], notices: [] }),
    );
  });
  return server;
}
