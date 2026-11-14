/** The warn shade of a cell with `count` mentions, against the busiest cell (`max`) of the table. */
export function heatShade(count: number, max: number): string {
  const strength = max > 0 ? Math.round((count / max) * 70) : 0;
  return `color-mix(in srgb, var(--color-warn) ${strength}%, transparent)`;
}
