import { useMemo } from 'react';
import type { VoiceFailure } from '../contracts';
import { recordFailure } from './silentHolds';

// Counts the failure when it first renders and returns how many silent holds in a row that makes. recordFailure
// counts one failure object once, so a second render or a strict-mode double render doesn't add a hold.
export function useSilentHolds(failure: VoiceFailure): number {
  return useMemo(() => recordFailure(failure), [failure]);
}
