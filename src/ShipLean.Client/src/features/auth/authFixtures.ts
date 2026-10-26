import { authTestIds as ids } from './testIds';
import type { MeDto } from '../../api/me';
import type { ProjectSummaryDto } from '../../api/projects';

// Stubs for the component tests of the auth feature (page.route answers; nothing here is production code).
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

export function projectSummary(key: string): ProjectSummaryDto {
  return { id: `id-${key}`, key, name: key, role: 'member', memberCount: 3 };
}

// The two things the helpers need of a Playwright page (its own type is not importable here).
interface StubbablePage {
  route(
    url: string,
    handler: (route: {
      fulfill(options: { status: number; contentType: string; body: string }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

interface TypablePage {
  getByTestId(id: string): { fill(value: string): Promise<void>; click(): Promise<void> };
}

export async function signInWithPassword(page: TypablePage, password = 'a-password') {
  await page.getByTestId(ids.email).fill('priya@shiplean.local');
  await page.getByTestId(ids.password).fill(password);
  await page.getByTestId(ids.signInSubmit).click();
}

export async function stubJson(page: StubbablePage, path: string, body: unknown, status = 200) {
  await page.route(`**${path}`, (route) =>
    route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) }),
  );
}

export async function stubInstance(page: StubbablePage) {
  await stubJson(page, '/api/auth/instance', { name: 'Larkspur' });
}

export async function stubProxyInstance(page: StubbablePage, signOutUrl: string | null = null) {
  await stubJson(page, '/api/auth/instance', { name: 'Larkspur', signIn: 'proxy', signOutUrl });
}
