import { expect, test } from 'vitest';
import { AVATAR_COLOURS, previewInitials } from './model';
// ShipLean.Core/People/AvatarPalette.cs: the server accepts only these. A copy here, so a palette change on either side
// fails this test until the other follows.
// accepts only these. A copy here, so a palette change on either side fails this test until the other follows.
const SERVER_PALETTE = [
  '#2f7fd8',
  '#16a089',
  '#7b4fc9',
  '#c9611a',
  '#b83d7a',
  '#3f7d5a',
  '#5a5fc9',
  '#8a5a2b',
  '#a3416b',
  '#52627c',
];

test('AvatarColours_ShouldMatchTheServersPalette', () => {
  expect([...AVATAR_COLOURS]).toEqual(SERVER_PALETTE);
});

test('previewInitials_WhenWordsAreSplitByAnyWhitespace_ShouldUseFirstAndLast', () => {
  expect(previewInitials('Ada Lovelace')).toBe('AL');
  expect(previewInitials('  ada \t byron   lovelace ')).toBe('AL');
});
