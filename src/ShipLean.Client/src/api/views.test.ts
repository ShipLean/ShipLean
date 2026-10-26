import { expect, test } from 'vitest';
import backlogTree from '../../../ShipLean.TestSupport/DtoFixtures/views/backlog-view.tree.json';
import boardFeatures from '../../../ShipLean.TestSupport/DtoFixtures/views/board-view.features.json';
import boardStories from '../../../ShipLean.TestSupport/DtoFixtures/views/board-view.stories.json';
import taskboard from '../../../ShipLean.TestSupport/DtoFixtures/views/taskboard-view.json';
import { BACKLOG_MODES, BACKLOG_ROW_KINDS, BOARD_LANE_KINDS } from './views';
import type { BacklogViewDto, BoardViewDto, TaskboardViewDto } from './views';

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

test('views_ShouldTypeEveryFixture', () => {
  const stories = typed<BoardViewDto>()(boardStories);
  const features = typed<BoardViewDto>()(boardFeatures);
  const tasks = typed<TaskboardViewDto>()(taskboard);
  const backlog = typed<BacklogViewDto>()(backlogTree);

  for (const lane of [...stories.lanes, ...features.lanes])
    expect(BOARD_LANE_KINDS).toContain(lane.kind);
  expect(BACKLOG_MODES).toContain(backlog.mode);
  for (const row of backlog.rows) expect(BACKLOG_ROW_KINDS).toContain(row.kind);
  expect(backlog.rows.at(-2)?.kind).toBe('noParentGroup');
  expect(backlog.forecast).toBeNull();
  expect(tasks.parentStates.map((s) => s.type)).toEqual(['story', 'bug']);
  expect(stories.columns.some((column) => column.hiddenCount > 0)).toBe(true);
  expect(features.lanes[0]?.epic?.key).toMatch(/^SHIP-/);
  expect(stories.columns.find((column) => column.id === 'col-in-review')?.overLimit).toBe(true);
  expect(features.lanes.at(-1)?.kind).toBe('noEpic');
  expect(tasks.rows.at(-1)?.parent).toBeNull();
  expect(tasks.peopleRows[0]?.person).toBeNull();
  expect(backlog.rows.some((row) => row.depth === 3)).toBe(true);
});
