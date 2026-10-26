import type { CardDto } from '../api/views';
import type { WorkItemDto } from '../api/workItems';
import type { CriteriaClause, CriteriaJson, FieldKind } from '../api/persisted';
import { criteriaField, ME } from './fieldDefinition';

/** A work item's field values by field id, in the JSON shapes of the field kind: null or absent when empty, an array for tags. */
export type FieldValues = Readonly<Record<string, unknown>>;

function isEmpty(value: unknown): boolean {
  return value === null || value === undefined || (Array.isArray(value) && value.length === 0);
}

function sameId(actual: unknown, wanted: unknown, me: string): boolean {
  const target = wanted === ME ? me : wanted;
  return (
    typeof actual === 'string' &&
    typeof target === 'string' &&
    actual.toLowerCase() === target.toLowerCase()
  );
}

function same(kind: FieldKind, actual: unknown, wanted: unknown, me: string): boolean {
  switch (kind) {
    case 'number':
    case 'points':
      return typeof actual === 'number' && typeof wanted === 'number' && actual === wanted;
    case 'user':
    case 'state':
    case 'sprint':
    case 'workItem':
      return sameId(actual, wanted, me);
    case 'text':
      return (
        typeof actual === 'string' &&
        typeof wanted === 'string' &&
        actual.toLowerCase() === wanted.toLowerCase()
      );
    default:
      return typeof actual === 'string' && actual === wanted;
  }
}

function compare(kind: FieldKind, actual: unknown, wanted: unknown): number | null {
  if (kind === 'date') {
    if (typeof actual !== 'string' || typeof wanted !== 'string') return null;
    return Date.parse(actual) - Date.parse(wanted);
  }
  return typeof actual === 'number' && typeof wanted === 'number' ? actual - wanted : null;
}

/** contains: a substring of text, a whole tag of tags (both ignoring case). */
function holds(kind: FieldKind, actual: unknown, wanted: unknown): boolean {
  if (typeof wanted !== 'string') return false;
  const text = wanted.toLowerCase();
  if (kind === 'tags') {
    return (
      Array.isArray(actual) && actual.some((t) => typeof t === 'string' && t.toLowerCase() === text)
    );
  }
  return typeof actual === 'string' && actual.toLowerCase().includes(text);
}

function clauseMatches(clause: CriteriaClause, item: FieldValues, me: string): boolean {
  const kind = criteriaField(clause.field)?.kind;
  if (!kind) throw new Error(`${clause.field} is not a criteria field.`);
  const actual = item[clause.field];
  const empty = isEmpty(actual);
  const { value } = clause;
  switch (clause.op) {
    case 'isEmpty':
      return empty;
    case 'isNotEmpty':
      return !empty;
    case '=':
      return !empty && same(kind, actual, value, me);
    case '<>':
      return empty || !same(kind, actual, value, me);
    case '>':
      return !empty && (compare(kind, actual, value) ?? 0) > 0;
    case '<':
      return !empty && (compare(kind, actual, value) ?? 0) < 0;
    case 'in':
      return (
        Array.isArray(value) &&
        value.some((e) => (e === null ? empty : !empty && same(kind, actual, e, me)))
      );
    case 'contains':
      return !empty && holds(kind, actual, value);
    case 'notContains':
      return empty || !holds(kind, actual, value);
  }
}

/** Whether the item satisfies valid criteria; `me` is the id `@me` stands for. The twin of the server's evaluator. */
export function matches(criteria: CriteriaJson, item: FieldValues, me: string): boolean {
  const test = (clause: CriteriaClause) => clauseMatches(clause, item, me);
  return criteria.match === 'all' ? criteria.clauses.every(test) : criteria.clauses.some(test);
}

function isWorkItem(dto: WorkItemDto | CardDto): dto is WorkItemDto {
  return 'priority' in dto;
}

/**
 * A work item or board card as field values. A card carries fewer fields than an item, so the ones it lacks
 * (priority, sprint, severity, who and when it was created or changed, original system) are absent, which reads as empty.
 *
 * Epic clauses are exact here only for a direct parent: a DTO carries its parent and not the parent's parent, so `epic` is set only
 * when the parent is an Epic and is absent otherwise (a feature under a feature, or a story under a feature, even when an Epic sits
 * higher up). The server's `epic` is the nearest Epic ancestor at any depth, so a screen that filters by Epic uses the server's results.
 */
export function fieldValuesOf(dto: WorkItemDto | CardDto): FieldValues {
  const epic = dto.parent?.type.id === 'epic' ? { epic: dto.parent.id } : {};
  const common = {
    id: dto.id,
    key: dto.key,
    type: dto.type.id,
    title: dto.title,
    state: dto.state.id,
    stateCategory: dto.state.category,
    assignee: dto.assignee?.id ?? null,
    points: dto.points,
    parent: dto.parent?.id ?? null,
    ...epic,
  };
  if (!isWorkItem(dto)) {
    return { ...common, tags: dto.tags.map((t) => t.name) };
  }
  return {
    ...common,
    tags: dto.tags,
    priority: dto.priority,
    severity: dto.severity,
    sprint: dto.sprint?.id ?? null,
    createdAt: dto.createdAt,
    createdBy: dto.createdBy?.id ?? null,
    changedAt: dto.changedAt,
    changedBy: dto.changedBy?.id ?? null,
    originalSystem: dto.originalSystem?.system ?? null,
    originalSystemId: dto.originalSystem?.id ?? null,
  };
}
