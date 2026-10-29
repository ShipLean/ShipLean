import type { BoardSettingsDto } from '../../../api/boards';
import type { BoardSettingsJson } from '../../../api/persisted';

// Stubs for the component tests of the board settings dialogs (page.route answers; nothing here is production code).
export const BOARD_ID = '00000000-0000-4000-8000-0000000000b1';
export const TASKBOARD_ID = '00000000-0000-4000-8000-0000000000b2';
export const PROJECT_ID = '00000000-0000-4000-8000-0000000000a1';

export function emptySettings(): BoardSettingsJson {
  return {
    columns: [],
    swimlanes: [],
    cardFields: [],
    styleRules: [],
    tagColours: [],
    annotations: { tasks: true, stories: true, bugs: true, features: true },
    cardReordering: null,
    transitions: [],
  };
}

export function boardSettingsDto(overrides: Partial<BoardSettingsDto> = {}): BoardSettingsDto {
  return {
    id: BOARD_ID,
    projectId: PROJECT_ID,
    level: 'stories',
    settings: emptySettings(),
    updatedAt: '2026-10-24T00:00:00Z',
    states: [],
    warnings: [],
    ...overrides,
  };
}
