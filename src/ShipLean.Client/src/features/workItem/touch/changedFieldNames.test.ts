import { expect, test } from 'vitest';
import { story } from '../ct/fixtures';
import { changedFields, draftOf } from '../form/draft';
import { changedFieldNames } from './changedFieldNames';

test('changedFieldNames_WhenPriorityAndPointsChanged_ShouldNameBoth', () => {
  const draft = { ...draftOf(story), priority: story.priority + 1, points: 13 };

  expect(changedFieldNames(changedFields(draftOf(story), draft))).toBe('Story points, Priority');
});

test('changedFieldNames_WhenNothingChanged_ShouldBeEmpty', () => {
  expect(changedFieldNames([])).toBe('');
});
