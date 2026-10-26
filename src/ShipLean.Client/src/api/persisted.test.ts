import { expect, test } from 'vitest';
import boardSettingsEpics from '../../../ShipLean.TestSupport/PinnedShapes/board-settings.epics.json';
import boardSettingsFeatures from '../../../ShipLean.TestSupport/PinnedShapes/board-settings.features.json';
import boardSettingsStories from '../../../ShipLean.TestSupport/PinnedShapes/board-settings.stories.json';
import boardSettingsTaskboard from '../../../ShipLean.TestSupport/PinnedShapes/board-settings.taskboard.json';
import criteriaAny from '../../../ShipLean.TestSupport/PinnedShapes/criteria.any.json';
import criteriaBlocked from '../../../ShipLean.TestSupport/PinnedShapes/criteria.blocked.json';
import criteriaCustomerBugs from '../../../ShipLean.TestSupport/PinnedShapes/criteria.customer-bugs.json';
import criteriaEmpty from '../../../ShipLean.TestSupport/PinnedShapes/criteria.empty.json';
import criteriaExpedite from '../../../ShipLean.TestSupport/PinnedShapes/criteria.expedite.json';
import criteriaIn from '../../../ShipLean.TestSupport/PinnedShapes/criteria.in.json';
import criteriaNotEqual from '../../../ShipLean.TestSupport/PinnedShapes/criteria.not-equal.json';
import criteriaNumeric from '../../../ShipLean.TestSupport/PinnedShapes/criteria.numeric.json';
import criteriaSpike from '../../../ShipLean.TestSupport/PinnedShapes/criteria.spike.json';
import criteriaText from '../../../ShipLean.TestSupport/PinnedShapes/criteria.text.json';
import criteriaWaitingOnReview from '../../../ShipLean.TestSupport/PinnedShapes/criteria.waiting-on-review.json';
import fieldChange from '../../../ShipLean.TestSupport/PinnedShapes/field-change.json';
import fieldIds from '../../../ShipLean.TestSupport/PinnedShapes/field-ids.json';
import importJobState from '../../../ShipLean.TestSupport/PinnedShapes/import-job-state.json';
import refs from '../../../ShipLean.TestSupport/PinnedShapes/refs.json';
import {
  CARD_REORDERINGS,
  CRITERIA_MATCHES,
  CRITERIA_OPS,
  FIELD_IDS,
  FIELD_KINDS,
  FIELD_USES,
  IMPORT_JOB_STATES,
  IMPORT_STEPS,
  LINK_TYPES,
  SWATCHES,
  SWIMLANE_KINDS,
  type BoardSettingsJson,
  type CriteriaJson,
  type FieldChange,
  type FieldDefinition,
  type ImportJobProgressJson,
  type ImportJobState,
  type ImportStep,
} from './persisted';
import {
  SPRINT_STATUSES,
  STATE_CATEGORIES,
  WORK_ITEM_TYPE_IDS,
  type SprintRef,
  type StateRef,
  type TypeRef,
  type UserRef,
} from './refs';

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

const criteriaDocuments = [
  typed<CriteriaJson>()(criteriaAny),
  typed<CriteriaJson>()(criteriaBlocked),
  typed<CriteriaJson>()(criteriaCustomerBugs),
  typed<CriteriaJson>()(criteriaEmpty),
  typed<CriteriaJson>()(criteriaExpedite),
  typed<CriteriaJson>()(criteriaIn),
  typed<CriteriaJson>()(criteriaNotEqual),
  typed<CriteriaJson>()(criteriaNumeric),
  typed<CriteriaJson>()(criteriaSpike),
  typed<CriteriaJson>()(criteriaText),
  typed<CriteriaJson>()(criteriaWaitingOnReview),
];

const boardDocuments = [
  typed<BoardSettingsJson>()(boardSettingsEpics),
  typed<BoardSettingsJson>()(boardSettingsFeatures),
  typed<BoardSettingsJson>()(boardSettingsStories),
  typed<BoardSettingsJson>()(boardSettingsTaskboard),
];

function criteriaOf(board: DeepWiden<BoardSettingsJson>): DeepWiden<CriteriaJson>[] {
  const swimlanes = board.swimlanes.flatMap((lane) => (lane.criteria ? [lane.criteria] : []));
  return [...swimlanes, ...board.styleRules.map((rule) => rule.criteria)];
}

test('persisted_ShouldTypeEveryFixture', () => {
  const criteria = criteriaDocuments;
  expect(criteria).toHaveLength(criteriaDocuments.length);
  const clauses = [...criteria, ...boardDocuments.flatMap(criteriaOf)].flatMap(
    (document) => document.clauses,
  );
  expect(clauses.length).toBeGreaterThan(0);
  expectAll(
    [...criteria, ...boardDocuments.flatMap(criteriaOf)].map((document) => document.match),
    CRITERIA_MATCHES,
  );
  expectAll(
    clauses.map((clause) => clause.op),
    CRITERIA_OPS,
  );
  const fieldIds = FIELD_IDS.map((field) => field.id);
  expectAll(
    clauses.map((clause) => clause.field),
    fieldIds,
  );

  const boards = boardDocuments;
  expect(boards).toHaveLength(boardDocuments.length);
  for (const board of boards) {
    expectAll(
      board.swimlanes.map((lane) => lane.kind),
      SWIMLANE_KINDS,
    );
    expectAll(
      [
        ...board.swimlanes.map((lane) => lane.colour),
        ...board.styleRules.map((rule) => rule.colour),
        ...board.tagColours.map((tag) => tag.colour),
      ],
      SWATCHES,
    );
    expectAll([board.cardReordering], CARD_REORDERINGS);
    expectAll(
      [
        ...board.cardFields.map((field) => field.type),
        ...board.columns.flatMap((column) => column.stateMappings.map((mapping) => mapping.type)),
      ],
      WORK_ITEM_TYPE_IDS,
    );
  }

  const revisions = fieldChange.revisions.map((revision) =>
    typed<FieldChange[]>()(revision.changes),
  );
  expect(revisions.length).toBeGreaterThan(0);
  expectAll(
    revisions.flat().map((change) => change.field),
    fieldIds,
  );
  const linkTypes = revisions
    .flat()
    .filter((change) => change.field === 'links')
    .flatMap((change): readonly unknown[] => (Array.isArray(change.to) ? change.to : []))
    .flatMap((item) =>
      typeof item === 'object' && item !== null && 'type' in item ? [String(item.type)] : [],
    );
  expect(linkTypes.length).toBeGreaterThan(0);
  expectAll(linkTypes, LINK_TYPES);

  const progress = Object.values(importJobState.progress).map((example) =>
    typed<ImportJobProgressJson>()(example.document),
  );
  expect(progress).toHaveLength(3);
  expectAll(
    progress.map((document) => document.step),
    IMPORT_STEPS,
  );
  expectAll(
    Object.values(importJobState.progress).map((example) => example.row.status),
    IMPORT_JOB_STATES,
  );
  expect(typed<ImportJobState[]>()(importJobState.importJobState)).toEqual([...IMPORT_JOB_STATES]);
  expect(typed<ImportStep[]>()(importJobState.importStep)).toEqual([...IMPORT_STEPS]);

  const users = typed<UserRef[]>()(refs.userRefs);
  expect(users).toHaveLength(6);
  const states = Object.values(refs.stateRefs).flatMap((list) => typed<StateRef[]>()(list));
  expectAll(
    states.map((state) => state.category),
    STATE_CATEGORIES,
  );
  expectAll(
    typed<SprintRef[]>()(refs.sprintRefs).map((sprint) => sprint.status),
    SPRINT_STATUSES,
  );
  expect(typed<TypeRef[]>()(refs.typeRefs).map((type) => type.id)).toEqual([...WORK_ITEM_TYPE_IDS]);
});

test('fieldIds_ShouldMatchTheVocabularyFixture', () => {
  const fixture = typed<FieldDefinition[]>()(fieldIds);

  expect(FIELD_IDS).toEqual(fixture);
  expectAll(
    fixture.map((field) => field.kind),
    FIELD_KINDS,
  );
  expectAll(
    fixture.flatMap((field) => field.usedIn),
    FIELD_USES,
  );
  expectAll(
    fixture.flatMap((field) => field.ops),
    CRITERIA_OPS,
  );
});
