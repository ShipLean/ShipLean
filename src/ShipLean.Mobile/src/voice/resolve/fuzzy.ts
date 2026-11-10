// Loose matching of what was said to the names and states on the screen: case, accents, spaces, hyphens, small
// slips of the ear and word forms. Each finder returns the one thing that fits, or nothing when none or several do.

// "José  Núñez-Ruiz" compares as "jose nunez ruiz".
export function plain(s: string): string {
  return s
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[\s-]+/g, ' ')
    .trim();
}

export function editDistance(a: string, b: string): number {
  let prev = Array.from({ length: b.length + 1 }, (_, j) => j);
  for (let i = 1; i <= a.length; i++) {
    const row = [i];
    for (let j = 1; j <= b.length; j++) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      row.push(Math.min(prev[j] + 1, row[j - 1] + 1, prev[j - 1] + cost));
    }
    prev = row;
  }
  return prev[b.length];
}

// How many slips a word of this length may carry: none up to three letters, one up to seven, then two.
const slips = (length: number) => (length <= 3 ? 0 : length <= 7 ? 1 : 2);

// The first tier that names exactly one item wins; a tier that names several stops the search with nothing.
function firstUnique<T>(items: T[], tiers: ((item: T) => boolean)[]): T | null {
  for (const fits of tiers) {
    const found = items.filter(fits);
    if (found.length === 1) return found[0];
    if (found.length > 1) return null;
  }
  return null;
}

// A person by full name, first name or surname. The first name is the name's first word, so "Mary-Ann" is whole.
export function findPerson<P extends { name: string }>(spoken: string, people: P[]): P | null {
  const heard = plain(spoken);
  if (heard === '') return null;
  const names = new Map(
    people.map((p) => {
      const words = p.name.trim().split(/\s+/);
      return [
        p,
        {
          full: plain(p.name),
          first: plain(words[0] ?? ''),
          last: words.length > 1 ? plain(words[words.length - 1]) : null,
        },
      ];
    }),
  );
  const of = (p: P) => names.get(p) ?? { full: '', first: '', last: null };
  const near = (name: string | null) =>
    name !== null && name !== '' && editDistance(heard, name) <= slips(name.length);
  return firstUnique(people, [
    (p) => of(p).full === heard,
    (p) => of(p).first === heard,
    (p) => of(p).last === heard,
    (p) => near(of(p).full) || near(of(p).first) || near(of(p).last),
  ]);
}

// "Resolved", "resolve" and "resolving" share a stem.
function stem(word: string): string {
  for (const suffix of ['ing', 'ed', 'es', 'e', 'd', 's']) {
    if (word.length > suffix.length + 2 && word.endsWith(suffix) && !word.endsWith('ss'))
      return word.slice(0, -suffix.length);
  }
  return word;
}

const squashed = (s: string) => plain(s).replace(/ /g, '');
const stemmed = (s: string) => plain(s).split(' ').map(stem).join('');

// A state of the item's type, by name or by another form of its words.
export function findState<S extends { name: string }>(spoken: string, states: S[]): S | null {
  if (squashed(spoken) === '') return null;
  return firstUnique(states, [
    (s) => squashed(s.name) === squashed(spoken),
    (s) => stemmed(s.name) === stemmed(spoken),
  ]);
}
