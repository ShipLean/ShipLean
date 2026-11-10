import { describe, expect, it } from 'vitest';
import { nudgeFor, nudgeTitle } from './nudge';

// What the work item's spec registers: keys, decision labels, state names and people.
const HOTWORDS = ['SHIP-906', 'D10', 'D11', 'New', 'Active', 'Done', 'Priya Raman'];

describe('nudgeFor', () => {
  it('nudges "approve D10 and set this to active" with 2 changes', () => {
    const nudge = nudgeFor('approve D10 and set this to active', HOTWORDS);
    expect(nudge).toEqual({ changes: ['approve D10', 'set this to active'] });
    expect(nudgeTitle(nudge!)).toBe('Looks like 2 changes');
  });

  it('does not nudge "the S22 runs warm"', () => {
    expect(nudgeFor('the S22 runs warm', HOTWORDS)).toBeNull();
  });

  it('needs a verb and a known word, not either alone', () => {
    // A verb with no word the screen knows.
    expect(nudgeFor('approve the plan and move on', HOTWORDS)).toBeNull();
    // Known words with no verb.
    expect(nudgeFor('D10 looks Active to Priya Raman', HOTWORDS)).toBeNull();
    // A known word inside another word doesn't count.
    expect(nudgeFor('approve D100', HOTWORDS)).toBeNull();
    // Both, ignoring case: one change.
    const one = nudgeFor('Give this to priya raman', HOTWORDS);
    expect(one).toEqual({ changes: ['Give this to priya raman'] });
    expect(nudgeTitle(one!)).toBe('Looks like a change');
  });
});
