/** A downward drag of the sheet's head longer than this (px) closes it on release. */
export const swipeCloseDistance = 80;
/** A downward flick faster than this (px per ms) closes the sheet on release, however short. */
export const swipeCloseSpeed = 0.5;

/** Whether a sheet dragged `dy` px downwards over `ms` milliseconds closes on release (otherwise it snaps back). */
export function shouldCloseOnRelease(dy: number, ms: number): boolean {
  if (dy <= 0) return false;
  if (dy >= swipeCloseDistance) return true;
  return ms > 0 && dy / ms > swipeCloseSpeed;
}
