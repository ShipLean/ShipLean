// The first run after sign-in: voice setup opens once, on a phone where it was never offered. The flag is per phone
// (outside any account), so signing out and in again doesn't offer it twice.
import type { KeyValueStore } from '@shiplean/core/platform';
import { engines } from '../voice/engines';

export const SETUP_OFFERED_KEY = 'shiplean.voice.setupOffered';

// Engines that aren't configured report no memory: there is nothing to set up, so nothing is offered.
export function shouldOfferSetup(kv: KeyValueStore): boolean {
  const { models } = engines();
  if (models.deviceMemory().reportedGb <= 0) return false;
  if (kv.getItem(SETUP_OFFERED_KEY) !== null) return false;
  return models.status().phase === 'none';
}

export function markSetupOffered(kv: KeyValueStore): void {
  kv.setItem(SETUP_OFFERED_KEY, '1');
}
