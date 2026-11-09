import { expect, test } from 'vitest';
import conflictHistory from '../../../ShipLean.TestSupport/DtoFixtures/workItems/conflict-history.json';
import bug from '../../../ShipLean.TestSupport/DtoFixtures/workItems/work-item.bug.json';
import feature from '../../../ShipLean.TestSupport/DtoFixtures/workItems/work-item.feature.json';
import story from '../../../ShipLean.TestSupport/DtoFixtures/workItems/work-item.story.json';
import { ApiRequestError } from './client';
import { LINK_TYPES } from './persisted';
import { STATE_CATEGORIES, WORK_ITEM_TYPE_IDS } from './refs';
import {
  REVISION_ORIGINS,
  type ConflictDto,
  type HistoryDto,
  type WorkItemChanges,
  type WorkItemDto,
  conflictOf,
} from './workItems';

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

function expectAll(values: readonly (string | null)[], allowed: readonly string[]) {
  for (const value of values) {
    if (value !== null) expect(allowed).toContain(value);
  }
}

test('workItems_ShouldTypeEveryFixture', () => {
  const items = [
    typed<WorkItemDto>()(story),
    typed<WorkItemDto>()(bug),
    typed<WorkItemDto>()(feature),
  ];
  const conflict = typed<ConflictDto>()(conflictHistory.conflict);
  const history = typed<HistoryDto>()(conflictHistory.history);

  const everyItem = [...items, conflict.current];
  expectAll(
    everyItem.flatMap((item) => [item.type.id, item.parent?.type.id ?? null]),
    WORK_ITEM_TYPE_IDS,
  );
  expectAll(
    everyItem.map((item) => item.state.category),
    STATE_CATEGORIES,
  );
  expectAll(
    everyItem.flatMap((item) => item.links.map((link) => link.type)),
    LINK_TYPES,
  );
  expectAll(
    [...history.revisions, ...conflict.theirChanges].map((revision) => revision.origin),
    REVISION_ORIGINS,
  );

  expect(conflict.code).toBe('conflict');
  expect(history.revisions.map((revision) => revision.agentName)).toContain(
    'Claude Code (claude.ai)',
  );
  expect(history.revisions.some((revision) => revision.agentName === null)).toBe(true);
  expect(history.revisions.map((revision) => revision.rev)).toEqual([6, 5, 4, 3, 2, 1]);
  expect(conflict.theirChanges.every((revision) => revision.rev > 6)).toBe(true);
  expect(items[1].originalSystem?.system).toBe('jira');
});

test('workItems_FeatureFixture_ShouldCarryTheFeatureFields', () => {
  const item = typed<WorkItemDto>()(feature);

  expect(item.type.id).toBe('feature');
  expect(item.goal).not.toBe('');
  expect(item.slug).toBe('checkout-v2');
  expect(item.startedOn).toBe('2026-10-12');
  expect(item.statusVerified).toMatchObject({ stale: false, via: 'set_status' });
  for (const other of [story, bug]) {
    expect(other.goal).toBe('');
    expect(other.statusVerified).toBeNull();
    expect(other.slug).toBeNull();
  }
});

test('featureWrites_ShouldBeOptionalOnTheWire', () => {
  const write: WorkItemChanges = {
    title: null,
    description: null,
    acceptanceCriteria: null,
    reproSteps: null,
    severity: null,
    state: null,
    assignee: null,
    sprint: null,
    priority: null,
    points: null,
    parent: null,
    tags: null,
    goal: { value: 'Two taps.' },
    statusLine: { value: 'Staging works.' },
    confirmStatus: true,
    addLinks: [],
    removeLinkIds: [],
  };
  expect(write.confirmStatus).toBe(true);
});

test('workItemChanges_ShouldTellUnchangedFromCleared', () => {
  const none: WorkItemChanges = {
    title: null,
    description: null,
    acceptanceCriteria: null,
    reproSteps: null,
    severity: null,
    state: null,
    assignee: null,
    sprint: null,
    priority: null,
    points: { value: null },
    parent: null,
    tags: null,
    addLinks: [],
    removeLinkIds: [],
  };
  expect(none.points).toEqual({ value: null });
  expect(none.priority).toBeNull();
});

test('conflictOf reads the ConflictDto a stale write answers, and nothing else', () => {
  const body = {
    error: 'Someone else changed it.',
    code: 'conflict',
    current: { id: 'x' },
    theirChanges: [],
  };
  expect(conflictOf(new ApiRequestError(body.error, 409, 'conflict', body))?.current).toEqual({
    id: 'x',
  });
  expect(
    conflictOf(
      new ApiRequestError('Taken.', 409, 'conflict', { error: 'Taken.', code: 'conflict' }),
    ),
  ).toBeUndefined();
  expect(conflictOf(new ApiRequestError('No.', 404, 'not_found'))).toBeUndefined();
  expect(conflictOf(new Error('boom'))).toBeUndefined();
});
