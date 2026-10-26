import type { BacklogViewDto } from '../../../api/views';
import type { SprintCreate, SprintDto } from '../../../api/sprints';
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

export interface BacklogServer {
  // The query strings of the backlog reads, in order.
  reads: string[];
  // The bodies of the work items created.
  creates: WorkItemCreate[];
  // The saved value of the `view:backlog` preference (undefined = nothing saved), and every value PUT to it.
  saved: () => unknown;
  puts: unknown[];
  setBacklog: (view: BacklogViewDto) => void;
  // The URLs and bodies of the reorders sent.
  reorders: { url: string; body: unknown }[];
  // The URLs and bodies of the work item PATCHes sent (a reparent).
  patches: { url: string; body: unknown }[];
  // The bodies of the velocity overrides PUT.
  overrides: unknown[];
  // The bodies of the sprints created.
  sprintCreates: SprintCreate[];
}

// Stubs everything the list makes: the backlog read, the `view:backlog` preference and the work item create.
export async function stubBacklog(
  page: Page,
  view: BacklogViewDto,
  options: {
    saved?: unknown;
    createStatus?: number;
    createBody?: unknown;
    // Answers a backlog read instead of `view` (a refused filter), or undefined to answer as usual.
    answer?: (search: URLSearchParams) => { status: number; body: unknown } | undefined;
    // Answers a reorder: one entry per call, the last one repeating; the default is a 200.
    reorderAnswers?: { status: number; body: unknown }[];
    // Answers a work item PATCH: one entry per call, the last one repeating; the default is a 200 at revision 5.
    patchAnswers?: { status: number; body: unknown }[];
    // The first PATCH is not answered until this settles.
    holdFirstPatch?: Promise<unknown>;
    // The project's sprints (GET) and the defaults for the next one; the default is none.
    sprints?: SprintDto[];
    nextSprint?: SprintCreate;
    // Answers the sprint create; the default is a 200 with the created sprint.
    sprintCreateAnswer?: { status: number; body: unknown };
  } = {},
): Promise<BacklogServer> {
  const server: BacklogServer = {
    reads: [],
    creates: [],
    puts: [],
    reorders: [],
    patches: [],
    overrides: [],
    sprintCreates: [],
    saved: () => saved,
    setBacklog: (next) => {
      current = next;
    },
  };
  let current = view;
  let saved = options.saved;
  await page.route('**/api/projects/p1/backlog?*', (route) => {
    const url = new URL(route.request().url());
    server.reads.push(url.search);
    const answer = options.answer?.(url.searchParams);
    return route.fulfill(answer ? json(answer.body, answer.status) : json(current));
  });
  await page.route('**/api/projects/p1/forecast', (route) => {
    server.overrides.push(route.request().postDataJSON());
    return route.fulfill({ status: 204 });
  });
  await page.route('**/api/projects/p1/members', (route) => route.fulfill(json([])));
  await page.route('**/api/projects/p1/states', (route) => route.fulfill(json([])));
  await page.route('**/api/projects/p1/sprints', (route) => {
    if (route.request().method() !== 'POST') return route.fulfill(json(options.sprints ?? []));
    const create = route.request().postDataJSON() as SprintCreate;
    server.sprintCreates.push(create);
    const answer = options.sprintCreateAnswer;
    return route.fulfill(json(answer?.body ?? { ...create, id: 'sp-new' }, answer?.status ?? 200));
  });
  await page.route('**/api/projects/p1/sprints/next', (route) =>
    route.fulfill(
      json(
        options.nextSprint ?? {
          name: 'Sprint 27',
          startDate: '2026-11-03',
          endDate: '2026-11-16',
          goal: '',
        },
      ),
    ),
  );
  await page.route('**/api/work-items/*/reorder', (route) => {
    const answers = options.reorderAnswers ?? [
      { status: 200, body: { id: 'x', key: 'x', revision: 2, changes: [], notices: [] } },
    ];
    const answer = answers[Math.min(server.reorders.length, answers.length - 1)];
    server.reorders.push({ url: route.request().url(), body: route.request().postDataJSON() });
    return route.fulfill(json(answer.body, answer.status));
  });
  await page.route('**/api/work-items/*', async (route) => {
    const answers = options.patchAnswers ?? [
      { status: 200, body: { id: 'x', key: 'x', revision: 5, changes: [], notices: [] } },
    ];
    const answer = answers[Math.min(server.patches.length, answers.length - 1)];
    server.patches.push({ url: route.request().url(), body: route.request().postDataJSON() });
    if (server.patches.length === 1) await options.holdFirstPatch;
    return route.fulfill(json(answer.body, answer.status));
  });
  await page.route('**/api/me/preferences/**', (route) => {
    if (route.request().method() === 'PUT') {
      saved = route.request().postDataJSON();
      server.puts.push(saved);
      return route.fulfill({ status: 204 });
    }
    return saved === undefined
      ? route.fulfill({ status: 204 })
      : route.fulfill(json({ key: 'view:backlog', json: saved }));
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
