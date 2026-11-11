// Keys that sound the same when said aloud: "nine sixty" and "nine oh six" (960/906), "thirteen" and "thirty"
// (13/30). A suggestion only ever comes from the keys on the screen (D6), and there is one or none, never a list.

// The two-digit pairs a listener confuses, each way round: "sixty" / "oh six" and "thirteen" / "thirty".
function confusions(pair: string): string[] {
  const [a, b] = pair;
  const out: string[] = [];
  if (b === '0' && a !== '0') out.push(`0${a}`);
  if (a === '0' && b !== '0') out.push(`${b}0`);
  if (a === '1' && b >= '3') out.push(`${b}0`);
  if (b === '0' && a >= '3') out.push(`1${a}`);
  return out;
}

// Every number one confusion away from `digits`, never with a leading zero.
export function soundAlikeNumbers(digits: string): string[] {
  const out = new Set<string>();
  for (let i = 0; i + 2 <= digits.length; i++) {
    for (const swap of confusions(digits.slice(i, i + 2))) {
      const n = digits.slice(0, i) + swap + digits.slice(i + 2);
      if (!n.startsWith('0') && n !== digits) out.add(n);
    }
  }
  return [...out];
}

const parts = (key: string) => /^([A-Za-z]+)-(\d+)$/.exec(key);

// The one candidate that sounds like `heard` (a key such as SHIP-960), or null when none does or more than one does.
export function suggestKey(heard: string, candidates: string[]): string | null {
  const h = parts(heard);
  if (h === null) return null;
  const alike = new Set(soundAlikeNumbers(h[2]));
  const found = candidates.filter((c) => {
    const p = parts(c);
    return p !== null && p[1].toLowerCase() === h[1].toLowerCase() && alike.has(p[2]);
  });
  const unique = [...new Set(found)];
  return unique.length === 1 ? unique[0] : null;
}
