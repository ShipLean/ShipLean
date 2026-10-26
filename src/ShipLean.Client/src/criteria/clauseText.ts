import type { CriteriaClause, CriteriaOp, FieldKind } from '../api/persisted';
import { criteriaField } from './fieldDefinition';
import { valueOptions, type PickLists } from './valueOptions';

const OP_LABELS: Record<CriteriaOp, string> = {
  '=': '=',
  '<>': '≠',
  '>': '>',
  '<': '<',
  in: 'in',
  contains: 'contains',
  notContains: 'not contains',
  isEmpty: 'is empty',
  isNotEmpty: 'is not empty',
};

/** How an operator reads in a clause; dates read as after / before. */
export function opLabel(op: CriteriaOp, kind: FieldKind): string {
  if (kind === 'date' && op === '>') return 'after';
  if (kind === 'date' && op === '<') return 'before';
  return OP_LABELS[op];
}

function scalarLabel(
  clause: CriteriaClause,
  element: string | number | null,
  lists: PickLists,
): string {
  if (element === null) return 'none';
  const field = criteriaField(clause.field);
  const options = field ? valueOptions(field, clause.op, lists) : null;
  return options?.find((o) => o.value === element)?.label ?? String(element);
}

/** The value of a clause as words: names for users, states and sprints, labels for fixed sets, "none" for an empty element of `in`. */
export function valueLabel(clause: CriteriaClause, lists: PickLists): string {
  const { value } = clause;
  if (value === null) return '';
  return Array.isArray(value)
    ? value.map((e) => scalarLabel(clause, e, lists)).join(', ')
    : scalarLabel(clause, value, lists);
}

/** One clause as a sentence: "Priority = 1", "Tags contains Blocked", "Points is empty". */
export function clauseText(clause: CriteriaClause, lists: PickLists): string {
  const field = criteriaField(clause.field);
  const parts = [
    field?.label ?? clause.field,
    opLabel(clause.op, field?.kind ?? 'text'),
    valueLabel(clause, lists),
  ];
  return parts.filter((p) => p !== '').join(' ');
}
