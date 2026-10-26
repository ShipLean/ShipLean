import type { BacklogViewDto } from '../../../api/views';
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

export interface LevelServer {
  // The path and query of each level read, in order.
  reads: string[];
  creates: WorkItemCreate[];
  // Every value PUT to the `view:backlog` preference.
  puts: unknown[];
}

// Stubs everything a level backlog makes: the level read, the `view:backlog` preference, the filter lists and the create.
export async function stubLevel(
  page: Page,
  view: BacklogViewDto,
  options: {
    saved?: unknown;
    // Answers a read instead of `view` (a refused filter), or undefined to answer as usual.
    answer?: (search: URLSearchParams) => { status: number; body: unknown } | undefined;
  } = {},
): Promise<LevelServer> {
  const server: LevelServer = { reads: [], creates: [], puts: [] };
  let saved = options.saved;
  await page.route('**/api/projects/p1/backlogs/*?*', (route) => {
    const url = new URL(route.request().url());
    server.reads.push(url.pathname + url.search);
    const answer = options.answer?.(url.searchParams);
    return route.fulfill(answer ? json(answer.body, answer.status) : json(view));
  });
  await page.route('**/api/projects/p1/members', (route) => route.fulfill(json([])));
  await page.route('**/api/projects/p1/states', (route) => route.fulfill(json([])));
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill(json([])));
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
      json({ id: 'new', key: 'SHIP-184', revision: 1, changes: [], notices: [] }),
    );
  });
  return server;
}
