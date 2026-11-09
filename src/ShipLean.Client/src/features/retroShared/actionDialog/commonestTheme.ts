import type { RetroPointDto, RetroThemeRefDto } from '@shiplean/core/api/retros';

/** The theme most of the points have; a tie goes to the point listed first. Points with no theme are ignored. */
export function commonestTheme(points: RetroPointDto[]): RetroThemeRefDto | null {
  const tally = new Map<string, { theme: RetroThemeRefDto; count: number }>();
  for (const point of points) {
    if (!point.theme) continue;
    const entry = tally.get(point.theme.id);
    if (entry) entry.count += 1;
    else tally.set(point.theme.id, { theme: point.theme, count: 1 });
  }
  let best: { theme: RetroThemeRefDto; count: number } | null = null;
  for (const entry of tally.values()) {
    if (best === null || entry.count > best.count) best = entry;
  }
  return best?.theme ?? null;
}
