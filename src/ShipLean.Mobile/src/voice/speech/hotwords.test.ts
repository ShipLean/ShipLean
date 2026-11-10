import { describe, expect, it } from 'vitest';
import { MAX_HOTWORDS, toHotwords } from './hotwords';

const lines = (text: string) => text.split('\n');

describe('hotwords', () => {
  it('SHIP-906 is offered as written and as spoken, without duplicates', () => {
    const text = toHotwords([
      'SHIP-906',
      'Priya',
      'In Progress',
      'ship-906',
      'SHIP-1040',
      'priya',
      ' In  Progress ',
    ]);

    expect(lines(text)).toEqual([
      'SHIP-906',
      'ship nine oh six',
      'Priya',
      'In Progress',
      'SHIP-1040',
      'ship one oh four oh',
    ]);
  });

  it('caps the list', () => {
    const keys = Array.from({ length: 60 }, (_, i) => `SHIP-${i + 1}`);

    const out = lines(toHotwords([...keys, 'Priya']));

    expect(out.length).toBeLessThanOrEqual(MAX_HOTWORDS);
    expect(out).toHaveLength(80);
    // The screen's order wins: the first 40 keys, each with its spoken form, and nothing after the cap.
    expect(out.slice(0, 4)).toEqual(['SHIP-1', 'ship one', 'SHIP-2', 'ship two']);
    expect(out.at(-2)).toBe('SHIP-40');
    expect(out).not.toContain('Priya');
  });

  it('keeps a key and its spoken form together at the cap', () => {
    const names = Array.from({ length: MAX_HOTWORDS - 1 }, (_, i) => `Name${i}`);

    const out = lines(toHotwords([...names, 'SHIP-7', 'Done']));

    expect(out).not.toContain('SHIP-7');
    expect(out.at(-1)).toBe('Done');
    expect(out).toHaveLength(MAX_HOTWORDS);
  });

  it('gives one phrase per line with no blank lines and no score suffix', () => {
    expect(toHotwords(['', '  ', 'Needs: review', 'Tom\nLee'])).toBe('Needs review\nTom Lee');
    expect(toHotwords([])).toBe('');
  });
});
