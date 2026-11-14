import { expect, test } from 'vitest';
import {
  DEFAULT_TIMELINE_OPTIONS,
  optionsFromQuery,
  optionsToQuery,
  selectedFromQuery,
  type TimelineOptions,
} from './TimelineOptions';

test('optionsFromQuery_WhenEmpty_ShouldGiveTheDefaults', () => {
  expect(optionsFromQuery('')).toEqual({
    group: 'stories',
    zoom: 'fit',
    deps: true,
    cp: true,
    view: 'chart',
  });
});

test('optionsFromQuery_WhenEveryOptionSet_ShouldParseThem', () => {
  expect(optionsFromQuery('?group=people&zoom=days&deps=off&cp=off&view=json')).toEqual({
    group: 'people',
    zoom: 'days',
    deps: false,
    cp: false,
    view: 'json',
  });
});

test('optionsFromQuery_WhenValuesUnknown_ShouldFallBackToTheDefaults', () => {
  expect(optionsFromQuery('?group=teams&zoom=weeks&deps=no&cp=maybe&view=table')).toEqual(
    DEFAULT_TIMELINE_OPTIONS,
  );
});

test('optionsToQuery_WhenRoundTripped_ShouldGiveTheSameOptions', () => {
  const options: TimelineOptions[] = [
    DEFAULT_TIMELINE_OPTIONS,
    { group: 'tasks', zoom: 'days', deps: false, cp: true, view: 'json' },
    { group: 'people', zoom: 'fit', deps: true, cp: false, view: 'chart' },
  ];
  for (const o of options) {
    expect(optionsFromQuery(optionsToQuery(o).toString())).toEqual(o);
  }
});

test('optionsToQuery_WhenDefaults_ShouldBeEmpty', () => {
  expect(optionsToQuery(DEFAULT_TIMELINE_OPTIONS).toString()).toBe('');
});

test('selectedFromQuery_WhenSelectSet_ShouldGiveTheKey', () => {
  expect(selectedFromQuery('?select=SHIP-192')).toBe('SHIP-192');
});

test('selectedFromQuery_WhenMissingOrEmpty_ShouldBeNull', () => {
  expect(selectedFromQuery('')).toBeNull();
  expect(selectedFromQuery('?select=')).toBeNull();
});
