import type { CriteriaClause, CriteriaOp, CriteriaValue, FieldDefinition } from '../api/persisted';
import { criteriaField } from './fieldDefinition';
import { valueOptions, type PickLists } from './valueOptions';

/** The value a clause starts with when its field or operator changes: valid where a sensible default exists. */
export function defaultValue(
  field: FieldDefinition,
  op: CriteriaOp,
  lists: PickLists,
): CriteriaValue {
  if (op === 'isEmpty' || op === 'isNotEmpty') return null;
  const first = valueOptions(field, op, lists)?.[0]?.value;
  if (op === 'in') return first === undefined ? [] : [first];
  if (op === 'contains' || op === 'notContains') return '';
  if (first !== undefined) return first;
  switch (field.kind) {
    case 'number':
    case 'points':
      return null;
    default:
      return '';
  }
}

/** A new clause on the field, with its first operator. */
export function newClause(fieldId: string, lists: PickLists): CriteriaClause {
  const field = criteriaField(fieldId);
  if (!field) throw new Error(`${fieldId} is not a criteria field.`);
  const op = field.ops[0];
  return { field: field.id, op, value: defaultValue(field, op, lists) };
}
