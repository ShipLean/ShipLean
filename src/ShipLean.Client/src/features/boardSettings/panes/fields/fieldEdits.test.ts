import { describe, expect, test } from 'vitest';
import type { CardFieldSettingJson } from '../../../../api/persisted';
import {
  addableFields,
  addExtra,
  extraFieldsFor,
  isFull,
  isOverLimit,
  moveExtraUp,
  removeExtra,
} from './fieldEdits';

const bug = (extraFields: string[]): CardFieldSettingJson => ({
  type: 'bug',
  showKey: true,
  showAssignee: true,
  showTags: true,
  showPoints: true,
  showEmptyFields: false,
  extraFields,
});

describe('fieldEdits', () => {
  test('extraFieldsFor_WhenBug_ShouldIncludeSeverityButNotForStory', () => {
    expect(extraFieldsFor('bug').map((f) => f.id)).toContain('severity');
    expect(extraFieldsFor('story').map((f) => f.id)).not.toContain('severity');
  });

  test('addExtra_WhenListed_ShouldAppendOnce', () => {
    const once = addExtra(bug([]), 'priority');
    expect(once.extraFields).toEqual(['priority']);
    expect(addExtra(once, 'priority')).toBe(once);
  });

  test('addExtra_WhenFull_ShouldRefuse', () => {
    const full = bug(Array.from({ length: 10 }, (_, i) => `f${i}`));
    expect(isFull(full)).toBe(true);
    expect(addExtra(full, 'priority')).toBe(full);
    expect(addableFields(bug(['priority'])).map((f) => f.id)).not.toContain('priority');
  });

  test('isOverLimit_WhenEleven_ShouldBeTrueAndTenNot', () => {
    expect(isOverLimit(bug(Array.from({ length: 11 }, (_, i) => `f${i}`)))).toBe(true);
    expect(isOverLimit(bug(Array.from({ length: 10 }, (_, i) => `f${i}`)))).toBe(false);
  });

  test('moveExtraUp_WhenFirst_ShouldDoNothingElseSwap', () => {
    const s = bug(['a', 'b', 'c']);
    expect(moveExtraUp(s, 0)).toBe(s);
    expect(moveExtraUp(s, 2).extraFields).toEqual(['a', 'c', 'b']);
  });

  test('removeExtra_WhenListed_ShouldDropIt', () => {
    expect(removeExtra(bug(['a', 'b']), 'a').extraFields).toEqual(['b']);
  });
});
