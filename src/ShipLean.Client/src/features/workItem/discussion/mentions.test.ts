import { describe, expect, it } from 'vitest';
import type { UserRef } from '../../../api/refs';
import { applyMention, mentionAt, suggestMentions } from './mentions';

const person = (id: string, name: string): UserRef => ({
  id,
  name,
  initials: name.slice(0, 2),
  avatarColour: '#000000',
  photoUrl: null,
});
const priya = person('1', 'Priya Nair');
const mei = person('2', 'Mei Lin');
const tom = person('3', 'Tom Nairn');

describe('mentionAt', () => {
  it('finds the name being typed after an @ at the start or after a space', () => {
    expect(mentionAt('@pr', 3)).toEqual({ start: 0, query: 'pr' });
    expect(mentionAt('Ask @na', 7)).toEqual({ start: 4, query: 'na' });
    expect(mentionAt('Ask @', 5)).toEqual({ start: 4, query: '' });
  });

  it('ignores an @ inside a word and text after the caret', () => {
    expect(mentionAt('mail me@pr', 10)).toBeNull();
    expect(mentionAt('@pr and more', 8)).toBeNull();
    expect(mentionAt('@pr and more', 3)).toEqual({ start: 0, query: 'pr' });
  });
});

describe('suggestMentions', () => {
  it('matches the start of any word of the name, ignoring case', () => {
    expect(suggestMentions([priya, mei, tom], 'nair')).toEqual([priya, tom]);
    expect(suggestMentions([priya, mei, tom], 'M')).toEqual([mei]);
  });

  it('suggests everyone for an empty query and no one for a stranger', () => {
    expect(suggestMentions([priya, mei], '')).toEqual([priya, mei]);
    expect(suggestMentions([priya, mei], 'zed')).toEqual([]);
  });
});

describe('applyMention', () => {
  it('replaces the typed query with the full name and a space, keeping the rest', () => {
    const text = 'Ask @pr about it';
    const mention = mentionAt(text, 7)!;
    expect(applyMention(text, 7, mention, priya)).toEqual({
      text: 'Ask @Priya Nair  about it',
      caret: 16,
    });
  });
});
