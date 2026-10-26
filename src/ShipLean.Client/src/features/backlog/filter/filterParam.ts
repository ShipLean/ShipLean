import type { CriteriaJson } from '../../../api/persisted';
import { checkClause } from '../../../criteria/validate';

export const NO_CRITERIA: CriteriaJson = { match: 'all', clauses: [] };

/**
 * The `filter` query value for the criteria: their JSON, or '' while there are no clauses or one is still half-typed,
 * which the builder flags itself. The list keeps sending the last whole one (ListMode holds it).
 */
export function filterParamOf(criteria: CriteriaJson): string {
  const whole =
    criteria.clauses.length > 0 && criteria.clauses.every((c, i) => checkClause(i, c) === null);
  return whole ? JSON.stringify(criteria) : '';
}

/** Whether the panel has clauses, so the list is narrowed and reordering is off (even while one is half-typed). */
export const isFiltering = (criteria: CriteriaJson): boolean => criteria.clauses.length > 0;
