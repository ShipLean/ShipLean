import { describe, expect, it } from 'vitest';
import { highlight } from './highlight';

const joined = (parts: { text: string }[]) => parts.map((p) => p.text).join('');
const matched = (parts: { text: string; matched: boolean }[]) =>
  parts.filter((p) => p.matched).map((p) => p.text);

describe('highlight', () => {
  const hotwords = ['SHIP-906', 'SHIP-907', 'Active', 'In Progress', 'Priya Nair'];

  it('matches SHIP-906 written as "ship 906" and a state regardless of case and hyphens', () => {
    const parts = highlight('move ship 906 above SHIP907 and set it to active', hotwords);
    expect(matched(parts)).toEqual(['ship 906', 'SHIP907', 'active']);
    expect(joined(parts)).toBe('move ship 906 above SHIP907 and set it to active');
    expect(matched(highlight('now in-progress please', hotwords))).toEqual(['in-progress']);
  });

  it('leaves words that are not hotwords plain and keeps punctuation out of a match', () => {
    const parts = highlight('Set this to Active, then wait', hotwords);
    expect(parts).toEqual([
      { text: 'Set this to ', matched: false },
      { text: 'Active', matched: true },
      { text: ', then wait', matched: false },
    ]);
  });

  it('does not light a number that only starts like a key', () => {
    expect(matched(highlight('ship 9060 is active-ish', hotwords))).toEqual([]);
  });

  it('lights a person by full name, and a first name only one person has', () => {
    expect(matched(highlight('ask priya nair', hotwords))).toEqual(['priya nair']);
    expect(matched(highlight('ask priya', hotwords))).toEqual(['priya']);
  });

  it('returns one plain part for text with no hotwords, and nothing for empty text', () => {
    expect(highlight('hello there', hotwords)).toEqual([{ text: 'hello there', matched: false }]);
    expect(highlight('', hotwords)).toEqual([]);
    expect(highlight('ship 906', [])).toEqual([{ text: 'ship 906', matched: false }]);
  });
});
