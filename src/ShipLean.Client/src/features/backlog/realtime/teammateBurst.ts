// More than BURST_ROWS row events within BURST_WINDOW_MS is one teammate action on many rows (a bulk move): the tab
// answers it with one toast and no per-row item reads.
export const BURST_ROWS = 3;
export const BURST_WINDOW_MS = 500;

/** The times (ms) still inside the window that ends at `now`. */
export function withinWindow(times: readonly number[], now: number): number[] {
  return times.filter((time) => now - time <= BURST_WINDOW_MS);
}

/** True when the times inside the window ending at `now` are more than three. */
export function isBurst(times: readonly number[], now: number): boolean {
  return withinWindow(times, now).length > BURST_ROWS;
}

export function burstText(count: number): string {
  return `${count === 1 ? '1 item' : `${count} items`} changed by a teammate`;
}
