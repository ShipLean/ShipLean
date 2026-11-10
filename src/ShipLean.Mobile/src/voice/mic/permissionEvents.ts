// "Read the microphone again now": the permission sheet says so after the phone's prompt was answered, and the
// watcher (which owns the comparison and the toast) listens.
const listeners = new Set<() => void>();

export function onMicRecheck(cb: () => void): () => void {
  listeners.add(cb);
  return () => {
    listeners.delete(cb);
  };
}

export function recheckMic(): void {
  for (const cb of [...listeners]) cb();
}
