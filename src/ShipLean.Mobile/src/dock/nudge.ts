// The nudge (typed D4): a note that reads like a command offers the preview instead. Pure and cheap: no model call.
// A part of the text counts as a change only when it has a command verb and one of the screen's own words (a key, a
// decision label, a state name or a person, from the registered spec's hotwords). Neither alone is enough, so
// "the S22 runs warm" never nudges.

const VERBS: readonly RegExp[] = [
  /\bapprove\b/i,
  /\bset\b.*\bto\b/i,
  /\bgive\b.*\bto\b/i,
  /\bassign\b/i,
  /\bmove\b/i,
];

// "approve D10 and set this to active" is two parts.
const PARTS = /\s*(?:[,;.]|\band\b|\bthen\b)\s*/i;

function escape(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function hasWord(part: string, hotwords: readonly string[]): boolean {
  return hotwords.some((h) => {
    const word = h.trim();
    if (word === '') return false;
    return new RegExp(`(^|[^\\p{L}\\p{N}])${escape(word)}($|[^\\p{L}\\p{N}])`, 'iu').test(part);
  });
}

export interface Nudge {
  // The parts of the text that read as changes, word for word, in order.
  changes: string[];
}

// Null when nothing in the text reads as a change.
export function nudgeFor(text: string, hotwords: readonly string[]): Nudge | null {
  const changes = text
    .split(PARTS)
    .filter((part) => part !== '' && VERBS.some((v) => v.test(part)) && hasWord(part, hotwords));
  return changes.length === 0 ? null : { changes };
}

// "Looks like 2 changes".
export function nudgeTitle(n: Nudge): string {
  return n.changes.length === 1 ? 'Looks like a change' : `Looks like ${n.changes.length} changes`;
}
