// How many silent holds in a row, kept outside React so it survives the sheet closing between holds.
import type { VoiceFailure } from '../contracts';

// The third silent hold in a row opens the mic check (voice D3).
export const SILENT_HOLDS_FOR_MIC_CHECK = 3;

// A pause this long between silent holds starts the count again.
export const SILENT_HOLDS_RESET_MS = 2 * 60 * 1000;

let count = 0;
let lastAt = 0;
let lastFailure: VoiceFailure | null = null;

// Counts a failure: nothingHeard adds one (after a long pause it starts again from one), anything else starts the
// count again. Recording the same failure object twice counts it once, so a re-render or a remounted effect doesn't
// add a hold. Returns the count including this failure.
export function recordFailure(failure: VoiceFailure, now: number = Date.now()): number {
  if (failure === lastFailure) return count;
  lastFailure = failure;
  if (failure.kind !== 'nothingHeard') {
    count = 0;
    return count;
  }
  if (count > 0 && now - lastAt > SILENT_HOLDS_RESET_MS) count = 0;
  count += 1;
  lastAt = now;
  return count;
}

// A hold that was understood (the voice provider calls this when a hold resolves to a preview or an answer), or the
// person left the mic check.
export function resetSilentHolds(): void {
  count = 0;
  lastAt = 0;
  lastFailure = null;
}

export function silentHolds(): number {
  return count;
}
