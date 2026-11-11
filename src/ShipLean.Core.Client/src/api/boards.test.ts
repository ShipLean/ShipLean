import { expect, test } from 'vitest';
import boardSettingsEpics from '../../../ShipLean.TestSupport/PinnedShapes/board-settings.epics.json';
import boardSettingsFeatures from '../../../ShipLean.TestSupport/PinnedShapes/board-settings.features.json';
import boardSettingsStories from '../../../ShipLean.TestSupport/PinnedShapes/board-settings.stories.json';
import boardSettingsTaskboard from '../../../ShipLean.TestSupport/PinnedShapes/board-settings.taskboard.json';
import putResult from '../../../ShipLean.TestSupport/DtoFixtures/boards/board-settings-put-result.json';
import {
  BOARD_LEVELS,
  BOARD_SECTIONS,
  BOARD_WARNING_KINDS,
  type BoardSettingsDto,
  type BoardSettingsPutResult,
} from './boards';

// Literal unions widened to their base type, recursively: what a JSON import looks like.
type DeepWiden<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends readonly (infer U)[]
        ? DeepWiden<U>[]
        : T extends object
          ? { [K in keyof T]: DeepWiden<T[K]> }
          : T;

// Maps every key the target type lacks to never, recursively, so a fixture property the type has dropped fails the check.
type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

// Typecheck fails if the fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

const pinned = [
  ['stories', boardSettingsStories],
  ['features', boardSettingsFeatures],
  ['epics', boardSettingsEpics],
  ['taskboard', boardSettingsTaskboard],
] as const;

test('boards_ShouldTypeThePutResultFixture', () => {
  const result = typed<BoardSettingsPutResult>()(putResult);
  expect(result.board.warnings.map((warning) => warning.kind)).toEqual(['unmappedState']);
  for (const kind of result.board.warnings.map((warning) => warning.kind)) {
    expect(BOARD_WARNING_KINDS).toContain(kind);
  }
  expect(BOARD_LEVELS).toContain(result.board.level);
  for (const section of result.mergedSections) expect(BOARD_SECTIONS).toContain(section);
});

test('Seed_BoardSettingsJson_ShouldDeserialiseThroughTheDto', () => {
  for (const [level, settings] of pinned) {
    const dto = typed<BoardSettingsDto>()({
      id: '00000007-0000-4000-8001-000000000001',
      projectId: '00000002-0000-4000-8000-000000000001',
      level,
      settings,
      updatedAt: '2026-10-24T09:30:00+00:00',
      states: [],
      warnings: [],
    });
    expect(Object.keys(dto.settings).sort()).toEqual([...BOARD_SECTIONS].sort());
  }
});
