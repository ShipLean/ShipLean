import { describe, expect, it } from 'vitest';
import { nothingHeardToast, TOO_SHORT_MS, tooShortToast, unclearCopy } from './heardCopy';

describe('heardCopy', () => {
  it('tooShort hints at holding for 3 seconds', () => {
    expect(tooShortToast()).toEqual({ text: 'Hold the mic while you speak', ms: TOO_SHORT_MS });
    expect(TOO_SHORT_MS).toBe(3000);
  });

  it('nothingHeard says nothing was changed and names the input only when it is known', () => {
    expect(nothingHeardToast(null).text).toBe("Didn't hear anything · nothing was changed");
    expect(nothingHeardToast('AirPods Pro').text).toBe(
      "Didn't hear anything · nothing was changed · AirPods Pro",
    );
  });

  it('unclear says Add as a note and Fix for voice, Post as a note and Edit for typed', () => {
    const voice = unclearCopy('voice');
    const typed = unclearCopy('typed');
    expect([voice.note, voice.fix]).toEqual(['Add as a note', 'Fix']);
    expect([typed.note, typed.fix]).toEqual(['Post as a note', 'Edit']);
    for (const c of [voice, typed]) {
      expect(c.nothingChanged).toBe('Nothing was changed');
      expect(c.whatCanISay).toBe('What can I say here?');
    }
  });
});
