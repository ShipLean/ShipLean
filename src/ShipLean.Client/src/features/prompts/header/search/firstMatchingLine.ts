/** A line of a prompt's text that holds the search, trimmed, with where the match sits in it. */
export interface MatchingLine {
  line: string;
  start: number;
  end: number;
}

/** The first line of `text` that holds `query` (case-insensitive, trimmed), or null when no line does. */
export function firstMatchingLine(text: string, query: string): MatchingLine | null {
  const needle = query.trim().toLowerCase();
  if (needle === '') return null;
  for (const raw of text.split('\n')) {
    const line = raw.trim();
    const start = line.toLowerCase().indexOf(needle);
    if (start >= 0) return { line, start, end: start + needle.length };
  }
  return null;
}

/** How much of the line before the match a results menu row keeps; more is cut to an ellipsis. */
const LEAD = 40;

/** The line split around the match, cut to at most 40 characters before it so the match stays in sight. */
export function lineExcerpt({ line, start, end }: MatchingLine): {
  before: string;
  match: string;
  after: string;
} {
  const from = Math.max(0, start - LEAD);
  return {
    before: (from > 0 ? '…' : '') + line.slice(from, start),
    match: line.slice(start, end),
    after: line.slice(end),
  };
}
