import { describe, expect, it } from 'vitest';
import { filterParamOf, isFiltering, NO_CRITERIA } from './filterParam';

describe('filterParamOf', () => {
  it('filterParamOf_WhenNoClauses_ShouldBeEmpty', () => {
    expect(filterParamOf(NO_CRITERIA)).toBe('');
    expect(isFiltering(NO_CRITERIA)).toBe(false);
  });

  it('filterParamOf_WhenWholeClause_ShouldBeTheCriteriaJson', () => {
    const criteria = {
      match: 'any' as const,
      clauses: [{ field: 'type', op: '=' as const, value: 'bug' }],
    };
    expect(JSON.parse(filterParamOf(criteria))).toEqual(criteria);
    expect(isFiltering(criteria)).toBe(true);
  });

  it('filterParamOf_WhenAClauseIsHalfTyped_ShouldBeEmpty', () => {
    const criteria = {
      match: 'all' as const,
      clauses: [
        { field: 'type', op: '=' as const, value: 'bug' },
        { field: 'title', op: 'contains' as const, value: 7 },
      ],
    };
    expect(filterParamOf(criteria)).toBe('');
    expect(isFiltering(criteria)).toBe(true);
  });
});
