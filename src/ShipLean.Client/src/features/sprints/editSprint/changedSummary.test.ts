import { describe, expect, it } from 'vitest';
import { changedSummary } from './changedSummary';

describe('changedSummary', () => {
  it('changedSummary_WhenOneFieldChanged_ShouldNameIt', () => {
    expect(changedSummary(['endDate'])).toBe('End date changed');
  });

  it('changedSummary_WhenSeveralFieldsChanged_ShouldListThemWithAnd', () => {
    expect(changedSummary(['name', 'endDate', 'goal'])).toBe('Name, end date and goal changed');
  });

  it('changedSummary_WhenNothingChanged_ShouldBeEmpty', () => {
    expect(changedSummary([])).toBe('');
  });
});
