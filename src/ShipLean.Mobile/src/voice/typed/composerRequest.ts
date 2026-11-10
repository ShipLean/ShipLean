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

// Forget a request nobody consumed: on sign-out, on a project switch, and between tests.
export function clearComposerRequest(): void {
  consume();
}

function consume(): void {
  if (current === null) return;
  current = null;
  notify();
}

// A dock is the composer's home on a screen. It registers while its screen is focused; with none registered, the
// request has no screen to open on and the sheet host (voice/typed/TypedCommandSheet) shows the composer instead.
let docks = 0;

export function registerDock(): () => void {
  docks += 1;
  notify();
  return () => {
    docks -= 1;
    notify();
  };
}

export function useDockFocused(): boolean {
  return useSyncExternalStore(subscribe, () => docks > 0);
}

// What "Type instead" asks for: Command, empty, with the mic-off reason line.
export function requestTypeInstead(): void {
  requestComposer({ mode: 'command', text: '', reason: 'micOff', questionNumber: null });
}

export function useComposerRequest(): { request: ComposerRequest | null; consume(): void } {
  const request = useSyncExternalStore(subscribe, () => current);
  return { request, consume };
}
