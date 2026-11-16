// Pure helpers for TouchPager: which page is on screen, from the track's scroll position and the page geometry.

/**
 * A page is the track's content width (inside its 12px padding) minus 26px, at most 340px: at 390px the next page peeks
 * 30px past the 8px gap. The same formula as the CSS `min()`.
 */
export function pageWidth(trackContentWidth: number): number {
  return Math.min(trackContentWidth - 26, 340);
}

/** The index of the page nearest the start edge for a scroll position, clamped to the pages that exist. */
export function pageInView(
  scrollLeft: number,
  pageWidthPx: number,
  gap: number,
  count: number,
): number {
  if (count <= 0) return 0;
  const stride = pageWidthPx + gap;
  const index = Math.round(scrollLeft / stride);
  return Math.min(Math.max(index, 0), count - 1);
}

/** The scroll position that puts a page at the start edge, held to what the track can actually scroll to. */
export function scrollTarget(
  index: number,
  pageWidthPx: number,
  gap: number,
  maxScroll: number,
): number {
  const wanted = index * (pageWidthPx + gap);
  return Math.min(Math.max(wanted, 0), Math.max(maxScroll, 0));
}
