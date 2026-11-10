// What the app remembers about the microphone and the screen, only to notice a change after the phone restarted the
// app (iPhone may, when a privacy switch changes). The stored permission is never the truth: it is compared with a
// fresh read and then replaced.
import type { KeyValueStore } from '@shiplean/core/platform';
import type { MicStatus } from '../internals';

const MIC_KEY = 'voice.mic.lastRead';
const ROUTE_KEY = 'voice.mic.lastRoute';

const STATES: readonly MicStatus[] = ['granted', 'undetermined', 'deniedCanAsk', 'deniedFinal'];

export function readLastMic(kv: KeyValueStore): MicStatus | null {
  const v = kv.getItem(MIC_KEY);
  return STATES.find((s) => s === v) ?? null;
}

export function writeLastMic(kv: KeyValueStore, status: MicStatus): void {
  kv.setItem(MIC_KEY, status);
}

export function readLastRoute(kv: KeyValueStore): string | null {
  return kv.getItem(ROUTE_KEY);
}

export function writeLastRoute(kv: KeyValueStore, route: string): void {
  kv.setItem(ROUTE_KEY, route);
}

// The microphone was off or unasked at the last read and is on now. Nothing stored means nothing to compare.
export function cameBackOn(before: MicStatus | null, now: MicStatus): boolean {
  return before !== null && before !== 'granted' && now === 'granted';
}
