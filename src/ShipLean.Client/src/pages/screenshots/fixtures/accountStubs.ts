import type { ConnectedAgentDto, ConsentRequestDto } from '../../../api/agents';
import type {
  InstanceProjectDto,
  InstanceSettingsDto,
  ProjectSummaryDto,
} from '../../../api/projects';
import type {
  AdminUserDto,
  AdminUsersDto,
} from '../../../features/settings/instanceUsers/usersAdminApi';
import { PEOPLE } from '../shellStub';
import type { Page } from '../shoot';

// Fixtures and stubs for the account and instance page screenshots (the design's Larkspur sample data).
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
);

export const MY_PROJECTS: ProjectSummaryDto[] = [
  ['p1', 'SHIP', 'Storefront', 'admin', 5],
  ['p-mob', 'MOB', 'Mobile App', 'admin', 4],
  ['p-pay', 'PAY', 'Payments', 'member', 6],
  ['p-data', 'DATA', 'Data Platform', 'member', 3],
  ['p-ops', 'OPS', 'Internal Tools', 'member', 3],
  ['p-part', 'PART', 'Partner Portal', 'member', 4],
].map(([id, key, name, role, memberCount]) => ({
  id: id as string,
  key: key as string,
  name: name as string,
  role: role as 'admin' | 'member',
  memberCount: memberCount as number,
}));

export async function stubPhoto(page: Page) {
  await page.route('**/api/users/**', (route) =>
    route.fulfill({ body: PNG, contentType: 'image/png' }),
  );
}

export async function stubInstanceSettings(page: Page) {
  const settings: InstanceSettingsDto = {
    name: 'Larkspur Engineering',
    baseUrl: 'https://shiplean.larkspur.dev',
    projectCreation: 'everyone',
  };
  await page.route('**/api/instance/settings', (route) => route.fulfill({ json: settings }));
}

const admin = (
  key: string,
  name: string,
  over: Partial<InstanceProjectDto> = {},
): InstanceProjectDto => ({
  id: `p-${key.toLowerCase()}`,
  key,
  name,
  memberCount: 4,
  admins: [PEOPLE.priya],
  hasActiveAdmin: true,
  itemCount: 640,
  yourRole: null,
  ...over,
});

export async function stubInstanceProjects(page: Page) {
  const projects: InstanceProjectDto[] = [
    admin('SHIP', 'Storefront', { memberCount: 5, itemCount: 183, yourRole: 'admin' }),
    admin('MOB', 'Mobile App', {
      admins: [PEOPLE.priya, PEOPLE.tom],
      itemCount: 412,
      yourRole: 'admin',
    }),
    admin('PAY', 'Payments', {
      memberCount: 6,
      admins: [PEOPLE.tom],
      itemCount: 1240,
      yourRole: 'member',
    }),
    admin('DATA', 'Data Platform', { memberCount: 3, admins: [PEOPLE.mei], itemCount: 305 }),
    admin('OPS', 'Internal Tools', { memberCount: 3, admins: [PEOPLE.tom], itemCount: 97 }),
    admin('PART', 'Partner Portal', { admins: [PEOPLE.ana], itemCount: 220, yourRole: 'member' }),
    admin('LEGACY', 'Legacy Billing', {
      admins: [],
      hasActiveAdmin: false,
      memberCount: 1,
      itemCount: 2100,
    }),
    admin('ARCH', 'Archive', { memberCount: 2, admins: [PEOPLE.sam], itemCount: 15 }),
  ];
  await page.route('**/api/instance/projects**', (route) => route.fulfill({ json: projects }));
}

const person = (
  id: string,
  name: string,
  email: string,
  colour: string,
  over: Partial<AdminUserDto> = {},
): AdminUserDto => ({
  id,
  name,
  email,
  initials: name
    .split(' ')
    .map((w) => w[0])
    .join(''),
  avatarColour: colour,
  photoUrl: null,
  isInstanceAdmin: false,
  status: 'active',
  lastSignIn: 'Yesterday',
  projects: [],
  ...over,
});

const inProject = (key: string, name: string, role: 'admin' | 'member' = 'member') => ({
  id: `p-${key.toLowerCase()}`,
  key,
  name,
  role,
  soleActiveAdmin: false,
});

export async function stubAdminUsers(page: Page) {
  const ship = inProject('SHIP', 'Storefront');
  const users: AdminUserDto[] = [
    person('00000001-0000-4000-8000-000000000002', 'Priya Shah', 'priya@larkspur.dev', '#2f7fd8', {
      isInstanceAdmin: true,
      lastSignIn: 'Now',
      projects: [
        inProject('SHIP', 'Storefront', 'admin'),
        inProject('PAY', 'Payments'),
        inProject('MOB', 'Mobile App', 'admin'),
        inProject('DATA', 'Data Platform'),
        inProject('OPS', 'Internal Tools'),
        inProject('PART', 'Partner Portal'),
      ],
    }),
    person('u-tom', 'Tom Okafor', 'tom@larkspur.dev', '#16a089', {
      isInstanceAdmin: true,
      lastSignIn: 'Today 09:12',
      projects: [ship, inProject('PAY', 'Payments'), inProject('OPS', 'Internal Tools')],
    }),
    person('u-mei', 'Mei Lin', 'mei@larkspur.dev', '#7b4fc9', {
      projects: [ship, inProject('DATA', 'Data Platform')],
    }),
    person('u-sam', 'Sam Rivera', 'sam@larkspur.dev', '#c9611a', {
      status: 'locked',
      lastSignIn: '3 days ago',
      projects: [ship, inProject('MOB', 'Mobile App')],
    }),
    person('u-ana', 'Ana Costa', 'ana@larkspur.dev', '#b83d7a', {
      lastSignIn: 'Today 08:40',
      projects: [ship, inProject('PART', 'Partner Portal')],
    }),
    person('u-ravi', 'Ravi Menon', 'ravi@larkspur.dev', '#5a5fd0', {
      status: 'oneTimePassword',
      lastSignIn: null,
      projects: [inProject('PAY', 'Payments')],
    }),
    person('u-dana', 'Dana Whitfield', 'dana@larkspur.dev', '#8a5a2b', {
      status: 'noPassword',
      lastSignIn: null,
      projects: [inProject('PART', 'Partner Portal')],
    }),
    person('u-jo', 'Jo Becker', 'jo@larkspur.dev', '#a33a6a', {
      lastSignIn: '2 weeks ago',
      projects: [inProject('MOB', 'Mobile App')],
    }),
    person('u-lee', 'Lee Park', 'lee@larkspur.dev', '#9a9a9a', {
      status: 'disabled',
      lastSignIn: '2 months ago',
      projects: [inProject('OPS', 'Internal Tools')],
    }),
  ];
  const body: AdminUsersDto = {
    users,
    administeredProjects: [
      { id: 'p1', key: 'SHIP', name: 'Storefront' },
      { id: 'p-mob', key: 'MOB', name: 'Mobile App' },
    ],
  };
  await page.route('**/api/admin/users**', (route) => route.fulfill({ json: body }));
}

export async function stubAgents(page: Page) {
  const agents: ConnectedAgentDto[] = [
    {
      id: 'a-1',
      clientName: 'Claude Code',
      clientHost: 'claude.ai',
      verified: true,
      scopes: ['shiplean:read', 'shiplean:write'],
      connectedAt: '2026-10-20T09:30:00+00:00',
      lastUsedAt: '2026-10-24T13:05:41+00:00',
      connectedLabel: '20 Oct',
      lastUsedLabel: '2h ago',
    },
  ];
  await page.route('**/api/me/agents', (route) => route.fulfill({ json: agents }));
  await page.route('**/api/agents/mcp-url', (route) =>
    route.fulfill({ json: { mcpUrl: 'https://shiplean.larkspur.dev/mcp' } }),
  );
}

export async function stubConsent(page: Page) {
  const request: ConsentRequestDto = {
    requestId: 'r-1',
    clientName: 'Claude Code',
    clientHost: 'claude.ai',
    verified: true,
    redirectHost: 'agent.example.test',
    requestedScopes: ['shiplean:read', 'shiplean:write', 'shiplean:plan'],
    localhostOnly: false,
  };
  await page.route('**/api/agents/consent/r-1', (route) => route.fulfill({ json: request }));
}

export async function stubNewProject(page: Page) {
  await page.route('**/api/projects/key-check**', (route) => {
    const key = new URL(route.request().url()).searchParams.get('key') ?? '';
    return route.fulfill({
      json: { key, available: true, reason: null, alternatives: ['CUSU', 'CSUP', 'CSHU'] },
    });
  });
  await page.route('**/api/projects/first-sprint**', (route) =>
    route.fulfill({
      json: { name: 'Sprint 1', startDate: '2026-10-26', endDate: '2026-11-08', goal: '' },
    }),
  );
  await page.route('**/api/users/by-email**', (route) =>
    route.fulfill({ json: { ...PEOPLE.mei } }),
  );
}
