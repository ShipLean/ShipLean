/** How far the typed velocity may sit from the recent sprints' before the page says so (a fraction of that velocity). */
export const VELOCITY_TOLERANCE = 0.1;

export type VelocityComparison =
  | { kind: 'none' }
  | { kind: 'inLine' }
  | { kind: 'above'; percent: number }
  | { kind: 'below'; percent: number };

/**
 * The velocity the team typed against the history (the project's override, else the recent sprints' average). Capacity
 * itself already follows velocity and days, so the check that is left is whether the typed velocity is a sensible one.
 * No rows, no typed velocity or no history is not a comparison. More than the tolerance either way is a warning;
 * exactly the tolerance is still in line.
 */
export function compareToVelocity(
  typedVelocity: number | undefined,
  entered: boolean,
  history: number,
): VelocityComparison {
  if (!entered || typedVelocity === undefined || history <= 0) return { kind: 'none' };
  const difference = (typedVelocity - history) / history;
  if (Math.abs(difference) <= VELOCITY_TOLERANCE) return { kind: 'inLine' };
  return difference > 0
    ? { kind: 'above', percent: Math.round(difference * 100) }
    : { kind: 'below', percent: Math.round(-difference * 100) };
}
