import { expect, test } from 'vitest';
import { colourOfTag, listedTags, withTagColour } from './tagEdits';

test('withTagColour_WhenCleared_ShouldDropTheEntry', () => {
  expect(withTagColour([{ tag: 'a', colour: 'ok' }], 'a', null)).toEqual([]);
});

test('withTagColour_WhenChanged_ShouldKeepOneEntryPerTag', () => {
  const next = withTagColour(
    [
      { tag: 'a', colour: 'ok' },
      { tag: 'b', colour: 'info' },
    ],
    'a',
    'warn',
  );
  expect(next).toEqual([
    { tag: 'b', colour: 'info' },
    { tag: 'a', colour: 'warn' },
  ]);
});

test('listedTags_WhenAColouredTagIsUnused_ShouldListItAfterTheProjectsTags', () => {
  expect(
    listedTags(
      ['x', 'y'],
      [
        { tag: 'gone', colour: 'ok' },
        { tag: 'x', colour: 'ok' },
      ],
    ),
  ).toEqual(['x', 'y', 'gone']);
});

test('withTagColour_WhenNameDiffersInCase_ShouldReplaceTheVariant', () => {
  expect(withTagColour([{ tag: 'blocked', colour: 'ok' }], 'Blocked', 'warn')).toEqual([
    { tag: 'Blocked', colour: 'warn' },
  ]);
});

test('colourOfTag_WhenNamesDifferInCase_ShouldTakeTheFirstEntry', () => {
  const colours = [
    { tag: 'blocked', colour: 'ok' },
    { tag: 'BLOCKED', colour: 'warn' },
  ] as const;
  expect(colourOfTag(colours, 'Blocked')).toBe('ok');
  expect(colourOfTag(colours, 'Other')).toBeNull();
});

test('listedTags_WhenAColouredTagDiffersInCase_ShouldNotListItAgain', () => {
  expect(listedTags(['Blocked'], [{ tag: 'blocked', colour: 'ok' }])).toEqual(['Blocked']);
});
