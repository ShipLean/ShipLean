import type { FeatureBoardViewDto } from '../../../api/views';
import type { TaskboardMoveRequest } from '../../taskboard/taskboardApi';
import { stubTaskboard, type TaskboardServer } from '../../taskboard/ct/stubApi';

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

export interface FeatureBoardServer extends TaskboardServer {
  // Every body POSTed to the Feature board moves endpoint.
  featureMoves: TaskboardMoveRequest[];
  // Every GET of the Feature board.
  featureReads: string[];
}

/** Stubs what the Feature board makes: the GET by key (or not_found for a key it does not know), the moves POST, and the Taskboard stub's preference, /api/me and work item routes. */
export async function stubFeatureBoard(
  page: Page,
  view: FeatureBoardViewDto,
  options: { knownKey?: string; saved?: unknown } = {},
): Promise<FeatureBoardServer> {
  const taskboard = await stubTaskboard(page, { ...view, sprintId: '' }, { saved: options.saved });
  const server: FeatureBoardServer = Object.assign(taskboard, {
    featureMoves: [] as TaskboardMoveRequest[],
    featureReads: [] as string[],
  });
  await page.route('**/api/projects/*/work-items/by-key/*/feature-board', (route) => {
    const key = decodeURIComponent(new URL(route.request().url()).pathname.split('/')[6]);
    server.featureReads.push(key);
    return route.fulfill(
      options.knownKey !== undefined && key !== options.knownKey
        ? json({ error: 'Not found.', code: 'not_found' }, 404)
        : json(view),
    );
  });
  await page.route('**/api/work-items/*/feature-board/moves', (route) => {
    server.featureMoves.push(route.request().postDataJSON() as TaskboardMoveRequest);
    return route.fulfill(
      json({
        written: { id: 'id', key: 'SHIP-173', revision: 2, changes: [], notices: [] },
        parentId: null,
        offerResolve: false,
      }),
    );
  });
  return server;
}
