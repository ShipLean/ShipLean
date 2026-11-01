import type { RetroCardDto, RetroPointDto } from '../../../../api/retros';

const holds = (text: string | null | undefined, q: string) =>
  text != null && text.toLowerCase().includes(q.toLowerCase());

/** Whether the point's own text or root cause holds the search text. */
export function pointMatches(point: RetroPointDto, q: string | undefined): boolean {
  return !q || holds(point.text, q) || holds(point.rootCause, q);
}

/** Whether the retro's raw failure log holds the search text. */
export function logMatches(card: RetroCardDto, q: string | undefined): boolean {
  if (!q) return false;
  const { blockers, assumptionsDisproved, friction } = card.failureLog;
  return [...blockers, ...assumptionsDisproved, ...friction].some((line) => holds(line, q));
}

/** A row that matched only through its retro's log (its text, theme and item don't hold the search text). */
export function matchedOnlyInLog(point: RetroPointDto, q: string | undefined): boolean {
  if (!q) return false;
  return (
    !pointMatches(point, q) &&
    !holds(point.theme?.name, q) &&
    !holds(point.workItem?.key, q) &&
    !holds(point.workItem?.title, q)
  );
}
