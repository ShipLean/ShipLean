// The touch breakpoint for script (K1, SHIP-805 rules 1 and 16). index.css's `touch:` and `phone:` variants use the
// same two media queries.
export type Viewport = 'phone' | 'tablet' | 'desktop';

export const TOUCH_QUERY = '(max-width: 900px)';
export const PHONE_QUERY = '(max-width: 599.98px)';

/** The viewport class of a window `width` CSS pixels wide: phone under 600, tablet from 600 to 900, desktop above. */
export function viewportOf(width: number): Viewport {
  if (width < 600) return 'phone';
  if (width <= 900) return 'tablet';
  return 'desktop';
}
