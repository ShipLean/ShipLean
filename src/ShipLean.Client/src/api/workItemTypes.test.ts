import { expect, test } from 'vitest';
import { WORK_ITEM_TYPE_IDS, type WorkItemTypeId } from './refs';
import { isStoryLevel, typeName } from './workItemTypes';

test.each<[WorkItemTypeId, boolean]>([
  ['story', true],
  ['bug', true],
  ['action', true],
  ['epic', false],
  ['feature', false],
  ['task', false],
])('isStoryLevel_When%s_ShouldBe%s', (type, expected) => {
  expect(isStoryLevel(type)).toBe(expected);
});

test('typeName_WhenAction_ShouldBeActionItem', () => {
  expect(typeName('action')).toBe('Action item');
});

test('typeName_WhenAnyType_ShouldBeNonEmpty', () => {
  for (const type of WORK_ITEM_TYPE_IDS) expect(typeName(type)).not.toBe('');
});
