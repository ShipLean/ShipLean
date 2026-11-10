// A work item key as it is said: "ship nine oh six", "ship 906", "SHIP906" and "ship dash nine zero six" all read
// as SHIP-906. Pure: no React, no network.

const UNITS: Record<string, number> = {
  zero: 0,
  oh: 0,
  o: 0,
  one: 1,
  two: 2,
  three: 3,
  four: 4,
  five: 5,
  six: 6,
  seven: 7,
  eight: 8,
  nine: 9,
};

const TEENS: Record<string, number> = {
  ten: 10,
  eleven: 11,
  twelve: 12,
  thirteen: 13,
  fourteen: 14,
  fifteen: 15,
  sixteen: 16,
  seventeen: 17,
  eighteen: 18,
  nineteen: 19,
};

const TENS: Record<string, number> = {
  twenty: 20,
  thirty: 30,
  forty: 40,
  fifty: 50,
  sixty: 60,
  seventy: 70,
  eighty: 80,
  ninety: 90,
};

const JOINERS = new Set(['dash', 'hyphen', 'minus']);

const isNumberWord = (t: string) =>
  /^\d+$/.test(t) || t in UNITS || t in TEENS || t in TENS || t === 'hundred';

// "ship906" splits into "ship" and "906"; hyphens and spaces separate words.
function tokens(spoken: string): string[] {
  return spoken
    .toLowerCase()
    .replace(/([a-z])(\d)/g, '$1 $2')
    .replace(/(\d)([a-z])/g, '$1 $2')
    .split(/[\s-]+/)
    .filter((t) => t !== '');
}

// The digits the number words spell, read the way people say keys: digit by digit ("nine oh six"), in pairs
// ("nine sixty six") or with a hundred ("nine hundred and six"). Null when a word isn't part of a number.
function digitsOf(words: string[]): string | null {
  let out = '';
  for (let i = 0; i < words.length; i++) {
    const w = words[i];
    if (/^\d+$/.test(w)) out += w;
    else if (w in UNITS) {
      if (words[i + 1] === 'hundred') {
        const rest = words.slice(i + 2).filter((t) => t !== 'and');
        const tail = rest.length === 0 ? '00' : digitsOf(rest);
        if (tail === null) return null;
        return out + String(UNITS[w]) + tail.padStart(2, '0');
      }
      out += String(UNITS[w]);
    } else if (w in TEENS) out += String(TEENS[w]);
    else if (w in TENS) {
      const next = words[i + 1];
      if (next !== undefined && next in UNITS && UNITS[next] > 0) {
        out += String(TENS[w] + UNITS[next]);
        i++;
      } else out += String(TENS[w]);
    } else return null;
  }
  return out === '' ? null : out;
}

// The key the words spell, upper case with one hyphen, or null when they don't spell a key: letters first, then
// a number, with "dash" or a hyphen allowed between.
export function readSpokenKey(spoken: string): string | null {
  const ts = tokens(spoken).filter((t) => !JOINERS.has(t));
  const first = ts.findIndex(isNumberWord);
  if (first <= 0) return null;
  const prefix = ts.slice(0, first).join('');
  if (!/^[a-z]+$/.test(prefix)) return null;
  const digits = digitsOf(ts.slice(first));
  if (digits === null) return null;
  return `${prefix.toUpperCase()}-${digits}`;
}
