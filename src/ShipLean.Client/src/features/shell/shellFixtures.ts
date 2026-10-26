import type { MeDto } from '../../api/me';
import type { ProjectSummaryDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';

// Stubs for the component tests of the shell (page.route answers; nothing here is production code).
export function meDto(overrides: Partial<MeDto> = {}): MeDto {
  return {
    id: '00000001-0000-4000-8000-000000000002',
    name: 'Priya Shah',
    email: 'priya@shiplean.local',
    initials: 'PS',
    avatarColour: '#3f7d5a',
    photoUrl: null,
    isInstanceAdmin: false,
    status: 'active',
    hasProjects: true,
    canCreateProjects: true,
    theme: 'os',
    density: 'compact',
    ...overrides,
  };
}

export function summary(key: string, name: string, memberCount = 3): ProjectSummaryDto {
  return { id: `id-${key}`, key, name, role: 'member', memberCount };
}

export const shellProjects = [
  summary('SHIP', 'Shipping', 5),
  summary('PAY', 'Payments'),
  summary('MOB', 'Mobile'),
];

export function sprint(overrides: Partial<SprintDto> = {}): SprintDto {
  return {
    id: 'sprint-24',
    name: 'Sprint 24',
    startDate: '2026-10-12',
    endDate: '2026-10-25',
    goal: '',
    status: 'current',
    rolloverStatus: null,
    datesLabel: '12 Oct – 25 Oct',
    timeLabel: '1 day left',
    ...overrides,
  };
}

interface Answerable {
  fulfill(options: { status: number; contentType?: string; body?: string }): Promise<void>;
  request(): { method(): string; postData(): string | null; url(): string };
}

// The two things the helpers need of a Playwright page (its own type is not importable here).
interface StubbablePage {
  route(url: string, handler: (route: Answerable) => Promise<void>): Promise<unknown>;
}

export interface ShellStubs {
  me?: MeDto;
  projects?: ProjectSummaryDto[];
  // The recentProjects preference as saved (project ids); undefined = nothing saved (204).
  recent?: string[];
  sprints?: SprintDto[];
  // Called with the body of each PUT of the recentProjects preference.
  onRecentSaved?: (ids: unknown) => void;
  // The PUTs of recentProjects wait for this before they are answered.
  holdSaves?: Promise<void>;
  // Count of the calls that fetch each project's by-key record (the real-time client's way into the hub).
  onByKeyRequested?: (url: string) => void;
  // Called on each GET of /api/projects.
  onProjectsRequested?: () => void;
  // Answer /api/me with a server error.
  meFails?: boolean;
}

const json = (route: Answerable, body: unknown) =>
  route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });

// Answers every call the shell makes.
export async function stubShell(page: StubbablePage, stubs: ShellStubs = {}) {
  const projects = stubs.projects ?? shellProjects;
  let recent = stubs.recent;
  await page.route('**/api/me', (route) =>
    stubs.meFails
      ? route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Nope.', code: 'validation' }),
        })
      : json(route, stubs.me ?? meDto()),
  );
  await page.route('**/api/projects', (route) => {
    stubs.onProjectsRequested?.();
    return json(route, projects);
  });
  // The real-time client resolves the key through this; the shell tests have no hub to join.
  await page.route('**/api/projects/by-key/*', (route) => {
    stubs.onByKeyRequested?.(route.request().url());
    return route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'No such project.', code: 'not_found' }),
    });
  });
  await page.route('**/api/projects/*/sprints', (route) =>
    json(route, stubs.sprints ?? [sprint()]),
  );
  await page.route('**/api/me/preferences/recentProjects', async (route) => {
    if (route.request().method() === 'PUT') {
      await stubs.holdSaves;
      recent = JSON.parse(route.request().postData() ?? '[]') as string[];
      stubs.onRecentSaved?.(recent);
      return route.fulfill({ status: 204 });
    }
    return recent === undefined
      ? route.fulfill({ status: 204 })
      : json(route, { key: 'recentProjects', json: recent });
  });
}
