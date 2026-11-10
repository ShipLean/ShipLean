// Pure: which point a spoken phrase means, and whether the person said "that".
import type { RetroPointDto } from '@shiplean/core/api/retros';

// Words that only point at a point; the rest of the phrase has to be in its text.
const FILLER = new Set(['the', 'one', 'point', 'about']);

const THAT = new Set(['that', 'that one', 'that point', 'this', 'this one', 'this point', 'it']);

const words = (s: string): string[] => s.toLowerCase().match(/[\p{L}\p{N}']+/gu) ?? [];

// "that", "this one" or "it": the point last touched (D8).
export function isThat(phrase: string): boolean {
  return THAT.has(words(phrase).join(' '));
}

// The points a phrase names: the one whose text is the phrase (case-insensitive), else every point whose text holds
// every word of the phrase, apart from "the", "one", "point" and "about". No words left means no match.
export function matchPoint(phrase: string, points: RetroPointDto[]): RetroPointDto[] {
  const said = words(phrase);
  const exact = points.filter((p) => words(p.text).join(' ') === said.join(' '));
  if (said.length > 0 && exact.length > 0) return exact;
  const wanted = said.filter((w) => !FILLER.has(w));
  if (wanted.length === 0) return [];
  return points.filter((p) => {
    const have = new Set(words(p.text));
    return wanted.every((w) => have.has(w));
  });
}
