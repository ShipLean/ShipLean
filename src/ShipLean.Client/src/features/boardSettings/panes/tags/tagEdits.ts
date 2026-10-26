import type { Swatch, TagColourJson } from '../../../../api/persisted';

const sameTag = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

/** A tag's colour as the server picks it: names compare ignoring case and the first entry wins. */
export function colourOfTag(tagColours: readonly TagColourJson[], tag: string): Swatch | null {
  return tagColours.find((t) => sameTag(t.tag, tag))?.colour ?? null;
}

/** The tags to list: the project's, then any coloured tag nobody uses any more (so its colour can still be cleared). */
export function listedTags(projectTags: readonly string[], tagColours: readonly TagColourJson[]) {
  const listed = [...projectTags];
  for (const { tag } of tagColours) {
    if (!listed.some((known) => sameTag(known, tag))) listed.push(tag);
  }
  return listed;
}

/** Sets a tag's colour, or clears it with null: a tag with no colour has no entry, and a case variant of the name counts as the same tag. */
export function withTagColour(
  tagColours: readonly TagColourJson[],
  tag: string,
  colour: Swatch | null,
): TagColourJson[] {
  const rest = tagColours.filter((t) => !sameTag(t.tag, tag));
  return colour === null ? rest : [...rest, { tag, colour }];
}
