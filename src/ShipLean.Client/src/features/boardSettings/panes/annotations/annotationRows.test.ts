import { describe, expect, test } from 'vitest';
import { annotationRowsFor, hasEffect } from './annotationRows';

describe('annotationRows', () => {
  test('annotationRowsFor_WhenStoriesOrTaskboard_ShouldNotListFeatures', () => {
    for (const level of ['stories', 'taskboard'] as const) {
      expect(annotationRowsFor(level).map((r) => r.key)).not.toContain('features');
    }
    expect(annotationRowsFor('features').map((r) => r.key)).toContain('features');
    expect(annotationRowsFor('epics').map((r) => r.key)).toContain('features');
  });

  test('hasEffect_WhenLevelHasNoSuchChildren_ShouldBeFalse', () => {
    expect(hasEffect('stories', 'tasks')).toBe(true);
    expect(hasEffect('stories', 'bugs')).toBe(false);
    expect(hasEffect('features', 'stories')).toBe(true);
    expect(hasEffect('features', 'tasks')).toBe(false);
    expect(hasEffect('features', 'features')).toBe(true);
    expect(hasEffect('epics', 'features')).toBe(true);
    expect(hasEffect('epics', 'stories')).toBe(true);
    expect(hasEffect('epics', 'bugs')).toBe(true);
    expect(hasEffect('epics', 'tasks')).toBe(false);
  });
});
