import { describe, expect, it } from 'vitest';
import {
  ambiguousCopy,
  candidateLabel,
  commonCopy,
  misheardKeyCopy,
  noMatchCopy,
  noValidActionCopy,
  webOnlyCopy,
} from './correctionCopy';

describe('correctionCopy', () => {
  it('always says nothing was changed, and Fix is Edit when typed', () => {
    expect(commonCopy('voice')).toMatchObject({
      nothingChanged: 'Nothing was changed',
      fix: 'Fix',
    });
    expect(commonCopy('typed').fix).toBe('Edit');
  });
  it('words a misheard key as one suggestion', () => {
    expect(misheardKeyCopy('SHIP-960', 'SHIP-906')).toMatchObject({
      lead: "There's no SHIP-960.",
      detail: 'Did you mean SHIP-906?',
      yes: 'Yes, SHIP-906',
    });
  });
  it('numbers candidates and offers the spoken number only to voice', () => {
    expect(candidateLabel(1, 'SHIP-906', 'Sign-in')).toBe('1 · SHIP-906 Sign-in');
    expect(ambiguousCopy('voice').detail).toContain('say its number');
    expect(ambiguousCopy('typed').detail).not.toContain('say');
  });
  it('words no match by what is on offer', () => {
    expect(noMatchCopy('x', 2, false).detail).toBe('Closest in this project:');
    expect(noMatchCopy('x', 2, true).detail).toBe('Closest on this screen:');
    expect(noMatchCopy('x', 0, true).detail).toContain('offline');
  });
  it('words each no valid action reason', () => {
    expect(noValidActionCopy('noSuchState', 3).detail).toBe('Pick one of these:');
    expect(noValidActionCopy('noSuchState', 0).detail).toBeNull();
    expect(noValidActionCopy('notAllowed', 3).lead).toContain("can't");
  });
  it('says where a web only action is done', () => {
    expect(webOnlyCopy('Tasks move on the web').detail).toBe('Tasks move on the web');
  });
});
