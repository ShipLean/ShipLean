import type { CriteriaValidationError } from '../api/criteria';
import type { CriteriaClause, CriteriaJson, FieldDefinition } from '../api/persisted';
import { criteriaField, ME } from './fieldDefinition';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// An ISO 8601 instant with its offset (Z or +hh:mm): without one, Date.parse reads local time and the server reads UTC.
const INSTANT = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$/;

function isDate(value: unknown): boolean {
  return typeof value === 'string' && INSTANT.test(value) && !Number.isNaN(Date.parse(value));
}

/** An Int32, the range of the integer columns (priority, severity). */
function isInt32(value: unknown): boolean {
  return (
    typeof value === 'number' && Number.isInteger(value) && value >= -(2 ** 31) && value < 2 ** 31
  );
}

/** At most two decimals and four integer digits: what the story_points column (numeric(6, 2)) holds. */
function isPoints(value: unknown): boolean {
  return (
    typeof value === 'number' &&
    Math.abs(value) <= 9999.99 &&
    Math.round(value * 100) / 100 === value
  );
}

function isId(value: unknown): boolean {
  return typeof value === 'string' && UUID.test(value);
}

function scalarOk(field: FieldDefinition, value: unknown): boolean {
  switch (field.kind) {
    case 'number':
      return isInt32(value);
    case 'points':
      return isPoints(value);
    case 'user':
      return value === ME || isId(value);
    case 'state':
    case 'sprint':
    case 'workItem':
      return isId(value);
    case 'stateCategory':
    case 'type':
      return typeof value === 'string' && (field.values ?? []).some((v) => v.value === value);
    case 'date':
      return isDate(value);
    default:
      return typeof value === 'string';
  }
}

function valueOk(field: FieldDefinition, clause: CriteriaClause): boolean {
  const { op, value } = clause;
  switch (op) {
    case 'isEmpty':
    case 'isNotEmpty':
      return value === null;
    case 'in':
      return (
        Array.isArray(value) &&
        value.length > 0 &&
        value.every((e) => (e === null ? field.nullable : scalarOk(field, e)))
      );
    case 'contains':
    case 'notContains':
      return typeof value === 'string';
    case '>':
    case '<':
      return scalarOk(field, value);
    default:
      return scalarOk(field, value);
  }
}

/** The local twin of the server's clause check: the field is a criteria field, the operator is one it allows, the value has the field's type. */
export function checkClause(index: number, clause: CriteriaClause): CriteriaValidationError | null {
  const field = criteriaField(clause.field);
  if (!field) {
    return {
      clauseIndex: index,
      field: clause.field,
      code: 'unknownField',
      message: `${clause.field} is not a field you can filter on.`,
    };
  }
  if (!field.ops.includes(clause.op)) {
    return {
      clauseIndex: index,
      field: clause.field,
      code: 'operatorNotAllowed',
      message: `${field.label} does not allow ${clause.op}.`,
    };
  }
  return valueOk(field, clause)
    ? null
    : {
        clauseIndex: index,
        field: clause.field,
        code: 'invalidValue',
        message: `The value is not valid for ${field.label}.`,
      };
}

/** Empty when the criteria are valid; the same codes as the server's Validate. */
export function checkCriteria(criteria: CriteriaJson): CriteriaValidationError[] {
  if (criteria.clauses.length === 0) {
    return [
      {
        clauseIndex: null,
        field: '',
        code: 'noClauses',
        message: 'Add at least one clause, or remove the criteria.',
      },
    ];
  }
  return criteria.clauses
    .map((clause, index) => checkClause(index, clause))
    .filter((e): e is CriteriaValidationError => e !== null);
}
