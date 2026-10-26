import type { CriteriaJson, StyleRuleJson } from '../../../../api/persisted';
import { newClause } from '../../../../criteria/defaultClause';
import type { PickLists } from '../../../../criteria/valueOptions';

export const newRuleId = () => `rule-${crypto.randomUUID().slice(0, 8)}`;

/** A rule that matches cards tagged with something the user picks next: the criteria always have a clause (the server refuses none). */
export function blankRule(lists: PickLists): StyleRuleJson {
  return {
    id: newRuleId(),
    name: 'New rule',
    enabled: true,
    colour: null,
    bold: false,
    italic: false,
    underline: false,
    criteria: { match: 'all', clauses: [newClause('tags', lists)] },
  };
}

export const replaceRule = (rules: StyleRuleJson[], next: StyleRuleJson): StyleRuleJson[] =>
  rules.map((r) => (r.id === next.id ? next : r));

export function moveRule(rules: StyleRuleJson[], from: number, to: number): StyleRuleJson[] {
  if (from === to || from < 0 || to < 0 || from >= rules.length || to >= rules.length) return rules;
  const next = [...rules];
  const [rule] = next.splice(from, 1);
  next.splice(to, 0, rule);
  return next;
}

export const withCriteria = (rule: StyleRuleJson, criteria: CriteriaJson): StyleRuleJson => ({
  ...rule,
  criteria,
});
