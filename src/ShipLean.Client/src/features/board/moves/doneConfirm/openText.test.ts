import { expect, test } from 'vitest';
import { boardOfChildren, openText } from './openText';

const rollups = [
  { type: 'feature' as const, open: 1, closed: 0 },
  { type: 'story' as const, open: 2, closed: 1 },
  { type: 'bug' as const, open: 1, closed: 3 },
];

test('openText_WhenSeveralTypesAreOpenOnFeatures_ShouldCountEachByType', () => {
  expect(openText(rollups, 'features')).toBe('2 stories, 1 bug and 1 nested Feature');
});

test('openText_WhenSeveralTypesAreOpenOnEpics_ShouldListFeaturesFirst', () => {
  expect(openText(rollups, 'epics')).toBe('1 Feature, 2 stories and 1 bug');
});

test('openText_WhenOneTypeHasOneOpen_ShouldUseTheSingular', () => {
  expect(openText([{ type: 'story', open: 1, closed: 0 }], 'features')).toBe('1 story');
});

test('openText_WhenNothingIsOpen_ShouldBeEmpty', () => {
  expect(openText([{ type: 'story', open: 0, closed: 4 }], 'features')).toBe('');
});

test('boardOfChildren_WhenEpics_ShouldBeTheFeaturesBoard', () => {
  expect(boardOfChildren('epics')).toBe('Features');
  expect(boardOfChildren('features')).toBe('Stories');
});
