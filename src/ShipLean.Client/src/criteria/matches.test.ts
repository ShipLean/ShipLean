import { describe, expect, test } from 'vitest';
import caseFile from '../../../ShipLean.TestSupport/Contracts/Criteria/criteria-contract-cases.json';
import type { CriteriaJson } from '../api/persisted';
import { CRITERIA_FIELDS } from './fieldDefinition';
import type { CardDto } from '../api/views';
import { fieldValuesOf, matches } from './matches';
import { checkCriteria } from './validate';

interface Case {
  name: string;
  criteria: unknown;
  item: Record<string, unknown>;
  valid: boolean;
  matches: boolean | null;
}

const cases = caseFile.cases as Case[];
const asCriteria = (c: Case) => c.criteria as CriteriaJson;

describe('criteria case file', () => {
  test.each(cases.filter((c) => c.valid).map((c) => [c.name, c] as const))(
    'matches: %s',
    (_name, c) => {
      expect(checkCriteria(asCriteria(c))).toEqual([]);
      expect(matches(asCriteria(c), c.item, caseFile.me)).toBe(c.matches);
    },
  );

  test.each(cases.filter((c) => !c.valid).map((c) => [c.name, c] as const))(
    'invalid: %s',
    (_name, c) => {
      expect(checkCriteria(asCriteria(c))).toHaveLength(1);
    },
  );
});

test('matches_WhenMeIsAnotherUser_ShouldNotMatchTheCaseFileUser', () => {
  const criteria: CriteriaJson = {
    match: 'all',
    clauses: [{ field: 'assignee', op: '=', value: '@me' }],
  };

  expect(matches(criteria, { assignee: caseFile.me }, caseFile.me)).toBe(true);
  expect(matches(criteria, { assignee: caseFile.me }, '00000001-0000-4000-8000-000000000002')).toBe(
    false,
  );
});

test('CRITERIA_FIELDS_ShouldListOnlyFieldsUsableInCriteria', () => {
  expect(CRITERIA_FIELDS).not.toContain('description');
  expect(CRITERIA_FIELDS).toContain('stateCategory');
});

const OTHER = '00000001-0000-4000-8000-000000000002';

test.each([
  ['type', 'bug', 'Bug'],
  ['stateCategory', 'inProgress', 'inprogress'],
])('matches_WhenAnExactFieldDiffersInCase_ShouldNotMatch: %s', (field, wanted, actual) => {
  const criteria: CriteriaJson = { match: 'all', clauses: [{ field, op: '=', value: wanted }] };

  expect(matches(criteria, { [field]: wanted }, OTHER)).toBe(true);
  expect(matches(criteria, { [field]: actual }, OTHER)).toBe(false);
});

test('matches_WhenGuidsDifferInHexCase_ShouldMatch', () => {
  const id = '0199f000-0000-7000-8000-000000000001';
  const criteria: CriteriaJson = {
    match: 'all',
    clauses: [{ field: 'assignee', op: '=', value: id }],
  };

  expect(matches(criteria, { assignee: id.toUpperCase() }, OTHER)).toBe(true);
});

function card(parentType: 'epic' | 'feature' | null): CardDto {
  return {
    id: 'c',
    key: 'SHIP-1',
    type: { id: 'story', name: 'Story' },
    title: 't',
    state: { id: 's', name: 'New', category: 'proposed' },
    assignee: null,
    points: null,
    childPoints: null,
    sumWarn: false,
    parent: parentType && {
      id: 'p',
      key: 'SHIP-2',
      title: 'p',
      type: { id: parentType, name: parentType },
    },
    tags: [],
    rollups: [],
    offerDone: false,
    styleRuleId: null,
    extraFields: [],
    revision: 1,
  } as CardDto;
}

describe('fieldValuesOf epic', () => {
  test('is the parent when the parent is an epic', () => {
    expect(fieldValuesOf(card('epic'))).toMatchObject({ epic: 'p' });
  });

  test.each([['feature' as const], [null]])(
    'is absent, not null, when the parent is %s',
    (parentType) => {
      expect('epic' in fieldValuesOf(card(parentType))).toBe(false);
    },
  );
});
