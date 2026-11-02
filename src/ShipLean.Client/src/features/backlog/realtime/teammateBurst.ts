// More than BURST_ROWS rows changed within BURST_WINDOW_MS is one teammate action on many rows (a bulk move): the tab
// answers it with one toast and no per-row item reads. Announcements wait for the window to close to find out.
export const BURST_ROWS = 3;
export const BURST_WINDOW_MS = 500;

/** True when more than three distinct rows changed in the window. */
export function isBurst(rowCount: number): boolean {
  return rowCount > BURST_ROWS;
}

export function burstText(count: number): string {
  return `${count === 1 ? '1 item' : `${count} items`} changed by a teammate`;
}
