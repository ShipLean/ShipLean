import type { BoardSettingsDto, BoardSettingsPut } from '../../../api/boards';
import type { BoardSettingsJson } from '../../../api/persisted';
import {
  boardSettingsDto,
  emptySettings,
  PROJECT_ID,
  TASKBOARD_ID,
} from '../../boardSettings/ct/boardSettingsFixtures';

interface StubbedRoute {
  request(): { method(): string; postDataJSON(): unknown };
  fulfill(response: { json: unknown }): Promise<void>;
}
export interface Routable {
  route(url: string, handler: (route: StubbedRoute) => Promise<void>): Promise<unknown>;
}

/** The Taskboard board with these settings on top of the empty ones. */
export const taskboardBoard = (settings: Partial<BoardSettingsJson>): BoardSettingsDto =>
  boardSettingsDto({
    id: TASKBOARD_ID,
    level: 'taskboard',
    settings: { ...emptySettings(), ...settings },
  });

/** CT only: answers the Taskboard settings dialog's calls (and the project's tags); returns the bodies PUT. */
export async function stubSettings(
  page: Routable,
  dto: BoardSettingsDto,
  tags: string[] = [],
  projectId: string = PROJECT_ID,
): Promise<BoardSettingsPut[]> {
  const puts: BoardSettingsPut[] = [];
  const answer = (path: string, json: unknown) =>
    page.route(`**/api/projects/${projectId}/${path}`, (route) => route.fulfill({ json }));
  await answer('boards', [{ id: TASKBOARD_ID, level: 'taskboard', name: 'Taskboard' }]);
  await answer('tags', tags);
  await answer('members', []);
  await answer('sprints', []);
  await page.route(`**/api/boards/${TASKBOARD_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: dto });
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    return route.fulfill({
      json: { board: { ...dto, settings: body.settings }, mergedSections: [] },
    });
  });
  return puts;
}
