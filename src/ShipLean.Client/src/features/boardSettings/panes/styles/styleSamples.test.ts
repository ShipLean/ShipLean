import { expect, test } from 'vitest';
import type { BoardLevel } from '../../../../api/boards';
import type { BoardSettingsJson, StyleRuleJson } from '../../../../api/persisted';
import { sampleOutcomes, unpreviewableRules, type Viewer } from './styleSamples';

const viewer: Viewer = {
  id: 'user-me',
  name: 'Joel Dickson',
  initials: 'JD',
  avatarColour: '#2563eb',
  photoUrl: null,
};

const rule = (
  id: string,
  field: string,
  op: string,
  value: string | number | null = null,
  enabled = true,
): StyleRuleJson => ({
  id,
  name: id,
  enabled,
  colour: 'danger',
  bold: false,
  italic: false,
  underline: false,
  criteria: {
    match: 'all',
    clauses: [{ field, op, value }],
  } as StyleRuleJson['criteria'],
});

const settings = (
  styleRules: StyleRuleJson[],
  tagColours: BoardSettingsJson['tagColours'] = [],
): BoardSettingsJson => ({
  columns: [],
  swimlanes: [],
  cardFields: [],
  styleRules,
  tagColours,
  annotations: { tasks: true, stories: true, bugs: true, features: true },
  cardReordering: null,
  transitions: [],
});

const outcome = (s: BoardSettingsJson, key: string, level: BoardLevel = 'stories') =>
  sampleOutcomes(s, level, viewer).find((o) => o.card.key === key)!;

test('sampleOutcomes_WhenTwoRulesMatch_ShouldStyleByTheFirst', () => {
  const s = settings([
    rule('blocked', 'tags', 'contains', 'Blocked'),
    rule('urgent', 'priority', '=', 1),
  ]);
  const result = outcome(s, 'KEY-129');
  expect(result.matched.map((r) => r.id)).toEqual(['blocked', 'urgent']);
  expect(result.card.styleRuleId).toBe('blocked');
});

test('sampleOutcomes_WhenRulesAreReordered_ShouldStyleByTheNewFirst', () => {
  const s = settings([
    rule('urgent', 'priority', '=', 1),
    rule('blocked', 'tags', 'contains', 'Blocked'),
  ]);
  expect(outcome(s, 'KEY-129').card.styleRuleId).toBe('urgent');
});

test('sampleOutcomes_WhenFirstRuleIsDisabled_ShouldFallToTheNext', () => {
  const s = settings([
    rule('blocked', 'tags', 'contains', 'Blocked', false),
    rule('urgent', 'priority', '=', 1),
  ]);
  expect(outcome(s, 'KEY-129').card.styleRuleId).toBe('urgent');
});

test('sampleOutcomes_WhenNoRuleMatches_ShouldLeaveTheCardPlain', () => {
  const s = settings([rule('spike', 'tags', 'contains', 'Spike')]);
  expect(outcome(s, 'KEY-141').card.styleRuleId).toBeNull();
});

test('sampleOutcomes_WhenClauseIsHalfTyped_ShouldMatchNothing', () => {
  const s = settings([rule('blank', 'priority', '<>', '')]);
  expect(outcome(s, 'KEY-129').matched).toEqual([]);
});

test('sampleOutcomes_ShouldColourTagsFromTagColours', () => {
  const s = settings([], [{ tag: 'Blocked', colour: 'warn' }]);
  expect(outcome(s, 'KEY-129').card.tags).toEqual([
    { name: 'Blocked', colour: 'warn' },
    { name: 'Security', colour: null },
  ]);
});

test('sampleOutcomes_WhenTagColoursDifferInCase_ShouldUseTheFirstEntry', () => {
  const s = settings(
    [],
    [
      { tag: 'blocked', colour: 'ok' },
      { tag: 'Blocked', colour: 'warn' },
    ],
  );
  expect(outcome(s, 'KEY-129').card.tags[0]).toEqual({ name: 'Blocked', colour: 'ok' });
});

test.each([
  ['changedBy', 'isNotEmpty', null],
  ['createdBy', 'isNotEmpty', null],
  ['changedAt', '>', '2026-01-01T00:00:00Z'],
  ['createdAt', '>', '2026-01-01T00:00:00Z'],
])('sampleOutcomes_WhenRuleReads_%s_ShouldMatchEverySample', (field, op, value) => {
  const s = settings([rule('r', field, op, value)]);
  expect(sampleOutcomes(s, 'stories', viewer).map((o) => o.card.styleRuleId)).toEqual([
    'r',
    'r',
    'r',
    'r',
  ]);
});

test('sampleOutcomes_WhenCreatedAtIsCompared_ShouldUseTheSampleDate', () => {
  const after = settings([rule('r', 'createdAt', '>', '2026-09-01T00:00:00Z')]);
  const before = settings([rule('r', 'createdAt', '<', '2026-09-01T00:00:00Z')]);
  expect(outcome(after, 'KEY-129').matched).toHaveLength(1);
  expect(outcome(before, 'KEY-129').matched).toEqual([]);
});

test('sampleOutcomes_WhenRuleIsAssigneeIsMe_ShouldMatchTheCardAssignedToTheViewer', () => {
  const s = settings([rule('mine', 'assignee', '=', '@me')]);
  const matched = sampleOutcomes(s, 'stories', viewer).filter((o) => o.matched.length > 0);
  expect(matched.map((o) => o.card.key)).toEqual(['KEY-129']);
  expect(matched[0].card.assignee?.id).toBe('user-me');
});

test('sampleOutcomes_WhenRuleReadsSeverity_ShouldMatchTheBug', () => {
  const s = settings([rule('sev', 'severity', '=', 2)]);
  expect(
    sampleOutcomes(s, 'stories', viewer).flatMap((o) => (o.matched.length ? [o.card.key] : [])),
  ).toEqual(['KEY-160']);
});

test.each([
  ['stories', ['story', 'story', 'bug', 'story']],
  ['features', ['feature', 'feature', 'feature', 'feature']],
  ['epics', ['epic', 'epic', 'epic', 'epic']],
] as const)('sampleOutcomes_WhenBoardIs_%s_ShouldSampleItsTypes', (level, types) => {
  const s = settings([rule('r', 'type', '=', types[0])]);
  const result = sampleOutcomes(s, level, viewer);
  expect(result.map((o) => o.card.type.id)).toEqual(types);
  expect(result[0].matched).toHaveLength(1);
});

test('unpreviewableRules_WhenRuleReadsOriginalSystem_ShouldNameIt', () => {
  const s = settings([rule('r', 'originalSystem', 'isEmpty'), rule('p', 'priority', '=', 1)]);
  expect(unpreviewableRules(s).map((u) => [u.rule.id, u.fields])).toEqual([
    ['r', ['originalSystem']],
  ]);
});
