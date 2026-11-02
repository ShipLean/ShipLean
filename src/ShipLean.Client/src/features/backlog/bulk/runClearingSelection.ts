import type { Selection } from '../selection/useSelection';
import type { BulkOutcome, BulkPlan, BulkRunner } from './useBulkRun';

/**
 * Runs a plan that moves rows and so ends the selection. Clears before the run: the outcome host re-selects the
 * rows a teammate changed once it resolves. A failed run wrote nothing, so the selection comes back. A plan with
 * nothing in it sends nothing and keeps the selection.
 */
export async function runClearingSelection(
  selection: Selection,
  runner: BulkRunner,
  plan: BulkPlan,
): Promise<BulkOutcome> {
  if (plan.items.length === 0) return runner.run(plan);
  const before = [...selection.ids];
  selection.clear();
  const outcome = await runner.run(plan);
  if (outcome.failed === true) selection.addMany(before);
  return outcome;
}
