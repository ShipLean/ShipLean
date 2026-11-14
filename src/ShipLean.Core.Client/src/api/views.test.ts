import { expect, test } from 'vitest';
import backlogTree from '../../../ShipLean.TestSupport/DtoFixtures/views/backlog-view.tree.json';
import boardFeatures from '../../../ShipLean.TestSupport/DtoFixtures/views/board-view.features.json';
import boardStories from '../../../ShipLean.TestSupport/DtoFixtures/views/board-view.stories.json';
import featureBoard from '../../../ShipLean.TestSupport/DtoFixtures/views/feature-board-view.json';
import timelineRunning from '../../../ShipLean.TestSupport/DtoFixtures/views/feature-timeline-running-view.json';
import timelineFinished from '../../../ShipLean.TestSupport/DtoFixtures/views/feature-timeline-view.json';
import taskboard from '../../../ShipLean.TestSupport/DtoFixtures/views/taskboard-view.json';
import { STATE_CATEGORIES } from './refs';
import {
  BACKLOG_MODES,
  BACKLOG_ROW_KINDS,
  BOARD_LANE_KINDS,
  KEY_DATE_POINTS,
  REVIEW_VERDICTS,
  TIMELINE_BAR_STATES,
  TIMELINE_EDGE_KINDS,
  TIMELINE_EDGE_SOURCES,
  TIMELINE_PERIOD_STATES,
} from './views';
import type {
  BacklogViewDto,
  BoardViewDto,
  FeatureBoardViewDto,
  FeatureTimelineViewDto,
  TaskboardViewDto,
} from './views';

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
  const feature = typed<FeatureBoardViewDto>()(featureBoard);

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
  expect(feature.sections.map((section) => section.depth)).toEqual([0, 1, 2]);
  expect(feature.rows.every((row) => feature.sections.some((s) => s.id === row.featureId))).toBe(
    true,
  );
  expect(feature.rows.some((row) => row.sprint === null)).toBe(true);
});

test('views_ShouldTypeTheTimelineFixtures', () => {
  const finished = typed<FeatureTimelineViewDto>()(timelineFinished);
  const running = typed<FeatureTimelineViewDto>()(timelineRunning);

  // JSON imports widen literal unions to string, so check every enum value against its union.
  for (const view of [finished, running]) {
    expect(STATE_CATEGORIES).toContain(view.feature.stateCategory);
    expect(view.keyDates.map((k) => k.point)).toEqual([...KEY_DATE_POINTS]);
    for (const gap of view.gaps) {
      expect(KEY_DATE_POINTS).toContain(gap.from);
      expect(KEY_DATE_POINTS).toContain(gap.to);
    }
    for (const story of view.stories) expect(['story', 'bug']).toContain(story.type);
    for (const bar of view.bars) {
      expect(bar.type).toBe('task');
      expect(TIMELINE_BAR_STATES).toContain(bar.state);
      for (const period of bar.history) expect(TIMELINE_PERIOD_STATES).toContain(period.state);
      for (const review of bar.reviews) expect(REVIEW_VERDICTS).toContain(review.verdict);
    }
    for (const edge of view.edges) {
      expect(TIMELINE_EDGE_KINDS).toContain(edge.kind);
      expect(TIMELINE_EDGE_SOURCES).toContain(edge.source);
    }
  }

  expect(finished.finished).toBe(true);
  expect(finished.scale.nowHours).toBeNull();
  expect(finished.criticalPath.length).toBeGreaterThan(0);
  expect(finished.bars.find((b) => b.key === 'SHIP-198')?.inferred).toBe(true);
  expect(finished.bars.find((b) => b.key === 'SHIP-192')?.reviews.map((r) => r.verdict)).toEqual([
    'changesRequested',
    'approved',
  ]);
  expect(running.finished).toBe(false);
  expect(running.criticalPath).toEqual([]);
  expect(running.keyDates[3]?.at).toBeNull();
  expect(running.gaps.at(-1)?.soFar).toBe(true);
  expect(running.bars.some((b) => b.state === 'inFlight' && b.end === null)).toBe(true);
});
