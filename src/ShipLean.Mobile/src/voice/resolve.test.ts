import { describe, expect, it } from 'vitest';
import { aTranscript } from '../test-support/voice';
import { copyNoteSpan, matchKey, matchPerson, matchState, unmatched } from './resolve';

const keys = ['SHIP-906', 'SHIP-960', 'SHIP-1091'];
const people = [
  { id: 'u1', name: 'Priya Shah' },
  { id: 'u2', name: 'Tom Reed' },
  { id: 'u3', name: 'Tom Hall' },
  { id: 'u4', name: 'Mary-Ann Lee' },
];
const states = [
  { id: 's1', name: 'Active' },
  { id: 's2', name: 'In progress' },
];

describe('matchKey', () => {
  it('matches the exact key', () => {
    expect(matchKey('SHIP-906', keys)).toEqual({ kind: 'match', value: 'SHIP-906' });
  });
  it('matches whatever the case', () => {
    expect(matchKey('ship-1091', keys)).toEqual({ kind: 'match', value: 'SHIP-1091' });
  });
  it('matches with a space or no hyphen', () => {
    expect(matchKey('ship 906', keys)).toEqual({ kind: 'match', value: 'SHIP-906' });
    expect(matchKey('SHIP960', keys)).toEqual({ kind: 'match', value: 'SHIP-960' });
  });
  it('finds none for a key that isn’t there', () => {
    expect(matchKey('ship 907', keys)).toEqual({ kind: 'none' });
    expect(matchKey('', keys)).toEqual({ kind: 'none' });
  });
});

describe('matchPerson', () => {
  it('matches the exact full name', () => {
    expect(matchPerson('Tom Reed', people)).toEqual({ kind: 'match', value: people[1] });
  });
  it('matches whatever the case, and a first name only one person has', () => {
    expect(matchPerson('priya shah', people)).toEqual({ kind: 'match', value: people[0] });
    expect(matchPerson('PRIYA', people)).toEqual({ kind: 'match', value: people[0] });
  });
  it('matches with a hyphen for a space', () => {
    expect(matchPerson('tom-hall', people)).toEqual({ kind: 'match', value: people[2] });
  });
  it('matches a hyphenated first name whole, and not by its first part', () => {
    expect(matchPerson('Mary-Ann', people)).toEqual({ kind: 'match', value: people[3] });
    expect(matchPerson('mary ann', people)).toEqual({ kind: 'match', value: people[3] });
    expect(matchPerson('Mary', people)).toEqual({ kind: 'none' });
  });
  it('finds none for a shared first name or a stranger', () => {
    expect(matchPerson('Tom', people)).toEqual({ kind: 'none' });
    expect(matchPerson('Ana', people)).toEqual({ kind: 'none' });
  });
});

describe('matchState', () => {
  it('matches the exact state', () => {
    expect(matchState('Active', states)).toEqual({ kind: 'match', value: states[0] });
  });
  it('matches whatever the case', () => {
    expect(matchState('active', states)).toEqual({ kind: 'match', value: states[0] });
  });
  it('matches with a hyphen or no space', () => {
    expect(matchState('in-progress', states)).toEqual({ kind: 'match', value: states[1] });
    expect(matchState('inprogress', states)).toEqual({ kind: 'match', value: states[1] });
  });
  it('finds none for a state the type doesn’t have', () => {
    expect(matchState('Done', states)).toEqual({ kind: 'none' });
  });
});

describe('copyNoteSpan', () => {
  it('copyNoteSpan copies the exact substring and refuses a span outside the transcript', () => {
    const t = aTranscript({ text: 'add a note: the API returns 500 on retry' });
    expect(copyNoteSpan(t, { start: 12, end: 40 })).toBe('the API returns 500 on retry');
    expect(copyNoteSpan(t, { start: 0, end: 3 })).toBe('add');
    expect(copyNoteSpan(t, { start: 12, end: 41 })).toBeNull();
    expect(copyNoteSpan(t, { start: -1, end: 3 })).toBeNull();
    expect(copyNoteSpan(t, { start: 5, end: 5 })).toBeNull();
  });
});

describe('unmatched', () => {
  it('turns none into noMatch and misheard into misheardKey', () => {
    expect(unmatched('ship 907', 'target item', { kind: 'none' })).toEqual({
      kind: 'noMatch',
      detail: { spoken: 'ship 907', role: 'target item', span: null },
    });
    expect(
      unmatched('ship 960', 'target item', {
        kind: 'misheard',
        heard: 'SHIP-960',
        suggestion: 'SHIP-906',
      }),
    ).toEqual({ kind: 'misheardKey', detail: { spoken: 'ship 960', suggestion: 'SHIP-906' } });
  });
});
