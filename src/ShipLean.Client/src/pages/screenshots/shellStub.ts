import type { MeDto } from '../../api/me';
import type { MemberDto, ProjectDto, ProjectSummaryDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import { meDto } from '../../features/auth/authFixtures';
import type { Page } from './shoot';

// The design's sample data (design/screens/shared.js): people, and Storefront (key SHIP, id p1 as the feature fixtures
// have it) with Sprints 21–26, Sprint 24 current.
export const PEOPLE = {
  priya: {
    id: 'u-priya',
    name: 'Priya Shah',
    initials: 'PS',
    avatarColour: '#2f7fd8',
    photoUrl: null,
  },
  tom: { id: 'u-tom', name: 'Tom Okafor', initials: 'TO', avatarColour: '#16a089', photoUrl: null },
  mei: { id: 'u-mei', name: 'Mei Lin', initials: 'ML', avatarColour: '#7b4fc9', photoUrl: null },
  sam: { id: 'u-sam', name: 'Sam Rivera', initials: 'SR', avatarColour: '#c9611a', photoUrl: null },
  ana: { id: 'u-ana', name: 'Ana Costa', initials: 'AC', avatarColour: '#b83d7a', photoUrl: null },
} as const;

export const SHELL_PROJECT: ProjectDto = {
  id: 'p1',
  key: 'SHIP',
  name: 'Storefront',
  description: '',
  role: 'admin',
  memberCount: 5,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 184,
};

function sprint(
  n: number,
  status: SprintDto['status'],
  startDate: string,
  endDate: string,
  datesLabel: string,
  timeLabel: string,
  goal = '',
): SprintDto {
  return {
    id: `sprint-${n}`,
    name: `Sprint ${n}`,
    startDate,
    endDate,
    goal,
    status,
    rolloverStatus: null,
    datesLabel,
    timeLabel,
  };
}

export const SHELL_SPRINTS: SprintDto[] = [
  sprint(21, 'past', '2026-09-07', '2026-09-18', '7 Sep – 18 Sep', 'Ended 5 weeks ago'),
  sprint(22, 'past', '2026-09-21', '2026-10-02', '21 Sep – 2 Oct', 'Ended 3 weeks ago'),
  sprint(23, 'past', '2026-10-05', '2026-10-16', '5 Oct – 16 Oct', 'Ended 9 days ago'),
  sprint(24, 'current', '2026-10-19', '2026-10-30', '19 Oct – 30 Oct', '5 days left'),
  sprint(25, 'future', '2026-11-02', '2026-11-13', '2 Nov – 13 Nov', 'Starts in 8 days'),
  sprint(26, 'future', '2026-11-16', '2026-11-27', '16 Nov – 27 Nov', 'Starts in 3 weeks'),
];

export const SHELL_MEMBERS: MemberDto[] = Object.values(PEOPLE).map((user) => ({
  user,
  email: `${user.id.slice(2)}@larkspur.dev`,
  role: user.id === PEOPLE.priya.id ? 'admin' : 'member',
  status: 'active',
}));

/** Each field replaces the default whole (an array is not merged); `me` and `project` are merged over the default. */
export interface ShellOverrides {
  /** The signed-in caller (default: Priya Shah, a project admin). */
  me?: Partial<MeDto>;
  /** The project `/api/projects/by-key/SHIP` answers (default: `SHELL_PROJECT`). */
  project?: Partial<ProjectDto>;
  /** The project picker's list (default: Storefront alone). */
  projects?: ProjectSummaryDto[];
  /** The project's sprints, in the order the API returns them (default: `SHELL_SPRINTS`). */
  sprints?: SprintDto[];
  /** The project's members (default: `SHELL_MEMBERS`). */
  members?: MemberDto[];
  /** Saved per-user preferences by key, e.g. `{ 'board.stories': {...} }`; a key not listed answers 204 (default: none). */
  preferences?: Record<string, unknown>;
  /** The instance name shown on the sign-in and shell (default: Larkspur). */
  instanceName?: string;
}

// The shell's reads for project SHIP. Registered first, so a feature's own stubs, registered after it, answer their
// routes (Playwright runs the last registered matching route first).
export async function stubShell(page: Page, overrides: ShellOverrides = {}) {
  const project: ProjectDto = { ...SHELL_PROJECT, ...overrides.project };
  const projects: ProjectSummaryDto[] = overrides.projects ?? [
    {
      id: project.id,
      key: project.key,
      name: project.name,
      role: project.role,
      memberCount: project.memberCount,
    },
  ];
  const sprints = overrides.sprints ?? SHELL_SPRINTS;
  const members = overrides.members ?? SHELL_MEMBERS;
  const preferences = overrides.preferences ?? {};
  await page.route('**/api/**', (route) => {
    const path = new URL(route.request().url()).pathname;
    const json = (body: unknown, status = 200) =>
      route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
    if (path === '/api/me') return json(meDto(overrides.me));
    if (path === '/api/projects') return json(projects);
    if (path === `/api/projects/by-key/${project.key}`) return json(project);
    if (path === `/api/projects/${project.id}/sprints`) return json(sprints);
    if (path === `/api/projects/${project.id}/members`) return json(members);
    if (path.startsWith('/api/me/preferences/')) {
      const key = decodeURIComponent(path.slice('/api/me/preferences/'.length));
      return key in preferences
        ? json({ key, json: preferences[key] })
        : route.fulfill({ status: 204 });
    }
    if (path === '/api/auth/instance') return json({ name: overrides.instanceName ?? 'Larkspur' });
    return json({ error: `Unstubbed ${path}`, code: 'internal_error' }, 500);
  });
}
