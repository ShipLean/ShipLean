/** How far the team's capacity may sit from velocity before the page says so (a fraction of velocity). */
export const VELOCITY_TOLERANCE = 0.1;

export type VelocityComparison =
  | { kind: 'none' }
  | { kind: 'inLine' }
  | { kind: 'above'; percent: number }
  | { kind: 'below'; percent: number };

/**
 * The team total against velocity. Nothing entered (or no velocity yet) is not a comparison. More than the tolerance
 * either way is a warning; exactly the tolerance is still in line.
 */
export function compareToVelocity(
  teamTotal: number,
  entered: boolean,
  velocity: number,
): VelocityComparison {
  if (!entered || velocity <= 0) return { kind: 'none' };
  const difference = (teamTotal - velocity) / velocity;
  if (Math.abs(difference) <= VELOCITY_TOLERANCE) return { kind: 'inLine' };
  return difference > 0
    ? { kind: 'above', percent: Math.round(difference * 100) }
    : { kind: 'below', percent: Math.round(-difference * 100) };
}
