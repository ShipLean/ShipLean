import { expect, test } from 'vitest';
import { parentTypesOf } from './ParentField';

test('parentTypesOf_WhenAction_ShouldOfferFeatures', () => {
  expect(parentTypesOf('action')).toEqual(['feature']);
});
