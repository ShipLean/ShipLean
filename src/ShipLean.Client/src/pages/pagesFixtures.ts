import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { meDto } from '../features/auth/authFixtures';

// Stubs for the component tests of the routes and pages (page.route answers; nothing here is production code).
export function projectDto(key: string): ProjectDto {
  return {
    id: `id-${key}`,
    key,
    name: key,
    description: '',
    role: 'admin',
    memberCount: 3,
    workingDays: [1, 2, 3, 4, 5],
    velocityWindow: 3,
    velocityOverride: null,
    nextItemNumber: 1,
  };
}

export function sprintDto(id: string, name = 'Sprint 24'): SprintDto {
  return {
    id,
    name,
    startDate: '2026-10-19',
    endDate: '2026-11-01',
    goal: '',
    status: 'current',
    rolloverStatus: null,
    datesLabel: '19 Oct – 1 Nov',
    timeLabel: '9 days left',
  };
}

// The slice of a Playwright page the stub needs (its own type is not importable here).
interface RoutablePage {
  route(
    url: string,
    handler: (route: {
      request(): { url(): string };
      fulfill(options: { status: number; contentType: string; body: string }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

// Answers every read the shell and the pages make: the caller, the project list, `project` by key (any other key is
// not found), and the project's sprints, members and preferences.
export async function stubProjectApi(
  page: RoutablePage,
  options: { key?: string; sprintIds?: string[] } = {},
) {
  const { key = 'SHIP', sprintIds = ['sprint-24'] } = options;
  await page.route('**/api/**', (route) => {
    const path = new URL(route.request().url()).pathname;
    const json = (body: unknown, status = 200) =>
      route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
    if (path === '/api/me') return json(meDto());
    if (path === '/api/projects') return json([]);
    if (path === `/api/projects/by-key/${key}`) return json(projectDto(key));
    if (path.startsWith('/api/projects/by-key/'))
      return json({ error: 'Not found.', code: 'not_found' }, 404);
    if (path.endsWith('/sprints')) return json(sprintIds.map((id) => sprintDto(id)));
    if (path.endsWith('/members')) return json([]);
    if (path.startsWith('/api/me/preferences/')) return json(null);
    if (path === '/api/auth/instance') return json({ name: 'Larkspur' });
    return json({ error: `Unstubbed ${path}`, code: 'internal_error' }, 500);
  });
}
