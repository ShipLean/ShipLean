import { expect, test } from 'vitest';
import type { StyleRuleJson } from '../../../../api/persisted';
import { blankRule, moveRule, replaceRule } from './ruleEdits';

const rule = (id: string): StyleRuleJson => ({ ...blankRule({}), id, name: id });

test('moveRule_WhenMovedDown_ShouldKeepTheOrderOfTheOthers', () => {
  const rules = [rule('a'), rule('b'), rule('c')];
  expect(moveRule(rules, 0, 1).map((r) => r.id)).toEqual(['b', 'a', 'c']);
});

test('moveRule_WhenOutOfRange_ShouldChangeNothing', () => {
  const rules = [rule('a'), rule('b')];
  expect(moveRule(rules, 0, 2)).toBe(rules);
  expect(moveRule(rules, -1, 0)).toBe(rules);
});

test('replaceRule_ShouldReplaceOnlyTheRuleWithThatId', () => {
  const rules = [rule('a'), rule('b')];
  const next = replaceRule(rules, { ...rules[1], enabled: false });
  expect(next.map((r) => r.enabled)).toEqual([true, false]);
});

test('blankRule_ShouldStartWithOneClauseAndBeEnabled', () => {
  const blank = blankRule({});
  expect(blank.enabled).toBe(true);
  expect(blank.criteria.clauses).toHaveLength(1);
});
