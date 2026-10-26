import type { ProjectEvent, ProjectEventType } from '../api/events';

// The events that come back to the tab that caused them: an import's progress and who is on a screen are not changes
// that tab made itself, so they are never filtered by clientId.
const ALWAYS_PASSES: readonly ProjectEventType[] = ['importProgress', 'presenceChanged'];

type Handler<T extends ProjectEventType> = (event: Extract<ProjectEvent, { type: T }>) => void;

export interface RemoteChanges {
  /** Delivers one event from the hub: skipped when this tab caused it (except import progress and presence). */
  handle(event: ProjectEvent): void;
  registerInvalidation<T extends ProjectEventType>(eventType: T, handler: Handler<T>): () => void;
  onRemote(handler: (event: ProjectEvent) => void): () => void;
  onReconnected(handler: (refetched: number) => void): () => void;
  /** After a reconnect: how many queries were invalidated. */
  reconnected(refetched: number): void;
}

// A fresh entry per registration, so registering one function twice takes two unregisters.
function register<A>(set: Set<(arg: A) => void>, handler: (arg: A) => void): () => void {
  const entry = (arg: A) => handler(arg);
  set.add(entry);
  return () => {
    set.delete(entry);
  };
}

export function createRemoteChanges(ownClientId: string): RemoteChanges {
  const byType = new Map<ProjectEventType, Set<(event: ProjectEvent) => void>>();
  const remote = new Set<(event: ProjectEvent) => void>();
  const reconnectListeners = new Set<(refetched: number) => void>();

  return {
    handle(event) {
      if (event.clientId === ownClientId && !ALWAYS_PASSES.includes(event.type)) return;
      for (const handler of [...remote]) handler(event);
      for (const handler of [...(byType.get(event.type) ?? [])]) handler(event);
    },
    registerInvalidation(eventType, handler) {
      let handlers = byType.get(eventType);
      if (handlers === undefined) {
        handlers = new Set();
        byType.set(eventType, handlers);
      }
      // Each handler is typed for its own event type; the set holds them all as handlers of any event.
      return register(handlers, handler as (event: ProjectEvent) => void);
    },
    onRemote: (handler) => register(remote, handler),
    onReconnected: (handler) => register(reconnectListeners, handler),
    reconnected(refetched) {
      for (const handler of [...reconnectListeners]) handler(refetched);
    },
  };
}
