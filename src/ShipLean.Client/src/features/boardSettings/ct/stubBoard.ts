import type { BoardSettingsDto, BoardSettingsPut } from '../../../api/boards';
import type { StyleRuleJson } from '../../../api/persisted';
import { BOARD_ID, PROJECT_ID } from './boardSettingsFixtures';

interface StubbedRoute {
  request(): { method(): string; postDataJSON(): unknown };
  fulfill(response: { json: unknown; status?: number }): Promise<void>;
}
export interface Routable {
  route(url: string, handler: (route: StubbedRoute) => Promise<void>): Promise<unknown>;
}

export const ME = {
  id: 'user-me',
  name: 'Joel Dickson',
  email: 'joel@example.com',
  initials: 'JD',
  avatarColour: '#2563eb',
  photoUrl: null,
  isInstanceAdmin: false,
  status: 'active',
  hasProjects: true,
  canCreateProjects: true,
  theme: 'os',
  density: 'comfortable',
};

/** CT only: answers the settings dialog's calls for one board, plus the project's tags; returns the bodies PUT. */
export async function stubBoard(
  page: Routable,
  dto: BoardSettingsDto,
  tags: string[] = [],
): Promise<BoardSettingsPut[]> {
  const puts: BoardSettingsPut[] = [];
  const answer = (path: string, json: unknown) =>
    page.route(`**/api/projects/${PROJECT_ID}/${path}`, (route) => route.fulfill({ json }));
  await answer('boards', [{ id: BOARD_ID, level: dto.level, name: dto.level }]);
  await answer('tags', tags);
  await page.route('**/api/me', (route) => route.fulfill({ json: ME }));
  await answer('members', []);
  await answer('sprints', []);
  await page.route(`**/api/boards/${BOARD_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: dto });
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    return route.fulfill({
      json: { board: { ...dto, settings: body.settings }, mergedSections: [] },
    });
  });
  return puts;
}

export const styleRule = (
  id: string,
  name: string,
  clause: { field: string; op: string; value: string | number | null },
  overrides: Partial<StyleRuleJson> = {},
): StyleRuleJson => ({
  id,
  name,
  enabled: true,
  colour: 'danger',
  bold: false,
  italic: false,
  underline: false,
  criteria: { match: 'all', clauses: [clause] } as StyleRuleJson['criteria'],
  ...overrides,
});
