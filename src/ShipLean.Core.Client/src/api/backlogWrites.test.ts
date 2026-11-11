import { expect, test } from 'vitest';
import addRequest from '../../../ShipLean.TestSupport/DtoFixtures/backlog/backlog-add-request.json';
import addResult from '../../../ShipLean.TestSupport/DtoFixtures/backlog/backlog-add-result.json';
import review from '../../../ShipLean.TestSupport/DtoFixtures/backlog/backlog-review.json';
import bulkRequest from '../../../ShipLean.TestSupport/DtoFixtures/backlog/bulk-write-request.json';
import bulkResult from '../../../ShipLean.TestSupport/DtoFixtures/backlog/bulk-write-result.json';
import duplicateRequest from '../../../ShipLean.TestSupport/DtoFixtures/backlog/duplicate-check-request.json';
import duplicateResult from '../../../ShipLean.TestSupport/DtoFixtures/backlog/duplicate-check-result.json';
import { BACKLOG_CHECK_IDS, BULK_MAX_ROWS, BULK_SLOTS } from './backlogWrites';
import type {
  BacklogAddRequest,
  BacklogAddResult,
  BacklogReviewDto,
  BulkWriteRequest,
  BulkWriteResult,
  DuplicateCheckRequest,
  DuplicateCheckResult,
} from './backlogWrites';

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

test('backlogWrites_ShouldTypeEveryFixture', () => {
  const bulk = typed<BulkWriteRequest>()(bulkRequest);
  const bulked = typed<BulkWriteResult>()(bulkResult);
  const add = typed<BacklogAddRequest>()(addRequest);
  const added = typed<BacklogAddResult>()(addResult);
  const duplicates = typed<DuplicateCheckRequest>()(duplicateRequest);
  const matches = typed<DuplicateCheckResult>()(duplicateResult);
  const reviewed = typed<BacklogReviewDto>()(review);

  for (const item of bulk.items)
    if (item.placement) expect(BULK_SLOTS).toContain(item.placement.slot);
  expect(bulked.stale[0]?.code).toBe('conflict');
  expect(add.placement?.slot).toBe('last');
  expect(added.created).toHaveLength(4);
  expect(duplicates.titles).toHaveLength(3);
  expect(matches.matches.map((m) => m.index)).toEqual([0, 2]);
  expect(reviewed.checks.map((c) => c.id)).toEqual([...BACKLOG_CHECK_IDS]);
});

test('backlogWrites_ShouldCapBulkAt100Rows', () => {
  expect(BULK_MAX_ROWS).toBe(100);
});
