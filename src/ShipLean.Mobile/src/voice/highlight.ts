// The listening sheet's partial text, split so the words that match a hotword can be shown matched.
import { matchKey, matchPerson, matchState } from './resolve';

export interface HighlightPart {
  text: string;
  matched: boolean;
}

// A hotword is a key, a state or a person's name: a phrase of up to this many spoken words ("ship 906", "in progress").
const MAX_WORDS = 4;

// Words keep their hyphens ("SHIP-906") and lose the punctuation around them.
const WORD = /[^\s,.;:!?"“”]+/g;

function isHotword(phrase: string, hotwords: string[]): boolean {
  const named = hotwords.map((h) => ({ id: h, name: h }));
  return (
    matchKey(phrase, hotwords).kind === 'match' ||
    matchState(phrase, named).kind === 'match' ||
    matchPerson(phrase, named).kind === 'match'
  );
}

// The parts, in order, joined back into exactly `partial`. A part is matched only when the exact matchers find one
// hotword for it; the longest phrase starting at a word wins.
export function highlight(partial: string, hotwords: string[]): HighlightPart[] {
  const unique = [...new Set(hotwords)];
  const words = [...partial.matchAll(WORD)].map((m) => ({
    start: m.index,
    end: m.index + m[0].length,
  }));
  const parts: HighlightPart[] = [];
  let cursor = 0;
  let i = 0;
  while (i < words.length) {
    let found = 0;
    for (let n = Math.min(MAX_WORDS, words.length - i); n >= 1; n--) {
      const phrase = partial.slice(words[i].start, words[i + n - 1].end);
      if (isHotword(phrase, unique)) {
        found = n;
        break;
      }
    }
    if (found === 0) {
      i += 1;
      continue;
    }
    const start = words[i].start;
    const end = words[i + found - 1].end;
    if (start > cursor) parts.push({ text: partial.slice(cursor, start), matched: false });
    parts.push({ text: partial.slice(start, end), matched: true });
    cursor = end;
    i += found;
  }
  if (cursor < partial.length) parts.push({ text: partial.slice(cursor), matched: false });
  return parts;
}
