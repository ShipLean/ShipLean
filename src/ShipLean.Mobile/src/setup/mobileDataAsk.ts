// Who has the "Continue on mobile data?" sheet open, and whether this download has been asked about. Banners on
// several mounted screens share one phone-wide flag in the kv store, so only the first to see the pause opens the
// sheet, and the flag clears when the download ends.
import type { KeyValueStore } from '@shiplean/core/platform';

export const MOBILE_DATA_ASKED_KEY = 'shiplean.voice.mobileDataAsked';

let openFor: string | null = null;
const listeners = new Set<() => void>();

function set(owner: string | null): void {
  if (openFor === owner) return;
  openFor = owner;
  for (const cb of [...listeners]) cb();
}

export const subscribeMobileDataAsk = (cb: () => void): (() => void) => {
  listeners.add(cb);
  return () => {
    listeners.delete(cb);
  };
};

export const mobileDataAskOwner = (): string | null => openFor;

// The download paused for Wi-Fi: opens the sheet for `owner` unless this download has asked already.
export function claimMobileDataAsk(kv: KeyValueStore, owner: string): void {
  if (kv.getItem(MOBILE_DATA_ASKED_KEY) !== null) return;
  kv.setItem(MOBILE_DATA_ASKED_KEY, '1');
  set(owner);
}

export function releaseMobileDataAsk(owner: string): void {
  if (openFor === owner) set(null);
}

// The download ended: the next pause is a new download.
export function endMobileDataAsk(kv: KeyValueStore): void {
  kv.removeItem(MOBILE_DATA_ASKED_KEY);
}
