// The screen's words (keys, names, states) in sherpa-onnx's hotwords format.
// The format is one phrase per line, in plain text as it should come out ("SHIP-906", "In Progress", "Priya"). For
// Parakeet the recognizer encodes each line with modeling_unit "bpe" and a bpe vocab; without them a line must
// already be tokens.txt pieces and plain words are skipped. A key goes in twice, as written and as spoken digit by
// digit ("ship nine oh six"), next to each other so the cap never splits them. Lines are de-duplicated ignoring case
// and spacing, in the screen's order, and capped at MAX_HOTWORDS: an entry that no longer fits is left out.
export const MAX_HOTWORDS = 80;

const KEY = /^([A-Za-z][A-Za-z0-9]*)-(\d+)$/;
const DIGITS = ['oh', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine'];

export function toHotwords(hotwords: string[]): string {
  const lines: string[] = [];
  const seen = new Set<string>();
  for (const hotword of hotwords) {
    // A colon followed by a number would be read as the line's own boost score, so none goes through.
    const phrase = hotword.replace(/:/g, ' ').replace(/\s+/g, ' ').trim();
    // sherpa-onnx rejects the whole file if a line has no letter ("42", "#1"), so such a line is dropped.
    if (!/\p{L}/u.test(phrase)) continue;
    const group = [phrase, ...spokenKey(phrase)].filter((line) => !seen.has(line.toLowerCase()));
    if (group.length === 0) continue;
    if (lines.length + group.length > MAX_HOTWORDS) continue;
    for (const line of group) {
      seen.add(line.toLowerCase());
      lines.push(line);
    }
  }
  return lines.join('\n');
}

// "SHIP-906" → ["ship nine oh six"]; anything that isn't a key → [].
function spokenKey(phrase: string): string[] {
  const key = KEY.exec(phrase);
  if (!key) return [];
  const digits = [...key[2]].map((d) => DIGITS[Number(d)]);
  return [[key[1].toLowerCase(), ...digits].join(' ')];
}
