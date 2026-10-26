import type { UserRef } from '../../../api/refs';

export interface MentionQuery {
  /** Where the `@` is in the text. */
  start: number;
  /** What was typed after it, up to the caret. */
  query: string;
}

/** The `@name` being typed at the caret, or null when the caret is not in one. An `@` counts only at the start or after whitespace. */
export function mentionAt(text: string, caret: number): MentionQuery | null {
  const found = /(^|\s)@([\p{L}]*)$/u.exec(text.slice(0, caret));
  if (!found) return null;
  const query = found[2] ?? '';
  return { start: caret - query.length - 1, query };
}

/** The people whose name has a word starting with `query` (any case), in the order given. */
export function suggestMentions(people: UserRef[], query: string): UserRef[] {
  const wanted = query.toLowerCase();
  return people.filter((person) =>
    person.name
      .toLowerCase()
      .split(/\s+/)
      .some((word) => word.startsWith(wanted)),
  );
}

/** The text with the `@query` at `mention` replaced by `@Full Name ` and where the caret goes. Mentions are plain text. */
export function applyMention(
  text: string,
  caret: number,
  mention: MentionQuery,
  person: UserRef,
): { text: string; caret: number } {
  const inserted = `@${person.name} `;
  return {
    text: text.slice(0, mention.start) + inserted + text.slice(caret),
    caret: mention.start + inserted.length,
  };
}
