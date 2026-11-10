import { describe, expect, it } from 'vitest';
import { soundAlikeNumbers, suggestKey } from './soundAlike';

describe('soundAlike', () => {
  it('suggests SHIP-906 for "SHIP-960" only when SHIP-906 is on the screen', () => {
    expect(suggestKey('SHIP-960', ['SHIP-906', 'SHIP-1091'])).toBe('SHIP-906');
    expect(suggestKey('SHIP-960', ['SHIP-1091', 'SHIP-12'])).toBeNull();
  });
  it('suggests nothing for a key with no sound-alike on the screen', () => {
    expect(suggestKey('SHIP-907', ['SHIP-906', 'SHIP-960', 'SHIP-1091'])).toBeNull();
    expect(suggestKey('SHIP-1234', ['SHIP-906'])).toBeNull();
  });
  it('confuses 960/906, 970/907, 13/30 and 15/50 each way round', () => {
    expect(suggestKey('SHIP-906', ['SHIP-960'])).toBe('SHIP-960');
    expect(suggestKey('SHIP-970', ['SHIP-907'])).toBe('SHIP-907');
    expect(suggestKey('SHIP-907', ['SHIP-970'])).toBe('SHIP-970');
    expect(suggestKey('SHIP-13', ['SHIP-30'])).toBe('SHIP-30');
    expect(suggestKey('SHIP-50', ['SHIP-15'])).toBe('SHIP-15');
  });
  it('suggests one key or none, never a list', () => {
    // "nine sixty" sounds like both "nine oh six" and "nine sixteen".
    expect(soundAlikeNumbers('960')).toEqual(expect.arrayContaining(['906', '916']));
    expect(suggestKey('SHIP-960', ['SHIP-906', 'SHIP-916'])).toBeNull();
  });
  it('only suggests a key from the same project', () => {
    expect(suggestKey('SHIP-960', ['CDX-906'])).toBeNull();
  });
});
