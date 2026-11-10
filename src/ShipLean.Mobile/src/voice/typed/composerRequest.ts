// A request to open the dock's composer, from anywhere voice offers "Type instead" (a failure, the mic sheet, a
// reply). The dock consumes it.
import { useSyncExternalStore } from 'react';

export type ComposerReason = 'micOff' | 'micCheck' | 'edit' | null;

export interface ComposerRequest {
  mode: 'note' | 'command' | 'reply';
  text: string;
  reason: ComposerReason;
  questionNumber: number | null;
}

let current: ComposerRequest | null = null;
const listeners = new Set<() => void>();

function notify(): void {
  for (const cb of [...listeners]) cb();
}

function subscribe(cb: () => void): () => void {
  listeners.add(cb);
  return () => {
    listeners.delete(cb);
  };
}

// The newest request replaces one not yet consumed.
export function requestComposer(req: ComposerRequest): void {
  current = req;
  notify();
}

function consume(): void {
  if (current === null) return;
  current = null;
  notify();
}

export function useComposerRequest(): { request: ComposerRequest | null; consume(): void } {
  const request = useSyncExternalStore(subscribe, () => current);
  return { request, consume };
}
