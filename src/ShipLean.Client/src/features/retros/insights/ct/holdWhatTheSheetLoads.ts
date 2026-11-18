import type { Page } from 'playwright';

// Shared by the theme sheet's shots in Themes.phone.spec and the page spec retroInsightsThemes.phone (SHIP-2110, SHIP-2121).

/**
 * Holds what a theme sheet loads (its mentions from Search, its open points) until the returned function is called. A
 * sheet that grows when they arrive is bottom-anchored with a fractional height, so it moves by a fraction of a pixel,
 * and the lines it had already painted keep their old pixel rounding (some land a pixel lower). An answer that raced the
 * sheet's first paint gave one shot or the other at random (SHIP-2110); answering after the paint gives the same one every run.
 */
export async function holdWhatTheSheetLoads(page: Page): Promise<() => void> {
  let answer!: () => void;
  const held = new Promise<void>((resolve) => (answer = resolve));
  await page.route(
    (url) => /^\/api\/projects\/[^/]+\/retros(\/action-dialog)?$/.test(url.pathname),
    async (route) => {
      await held;
      await route.fallback();
    },
  );
  return answer;
}

/** Resolves once the browser has painted the current page: a frame callback runs before a paint, its next one after it. */
export const afterTheNextPaint = (page: Page) =>
  page.evaluate(
    () => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))),
  );
