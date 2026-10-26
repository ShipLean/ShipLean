import type { ProjectEvent } from '../api/events';
import { routeIds, type RouteId } from '../routes';
import type { RealtimeConnection } from './connection';
import type { ConnectionState } from './connectionState';
import { createProjectSubscription, type ProjectSubscription } from './projectSubscription';
import { createRemoteChanges, type RemoteChanges } from './remoteChanges';
import { createStore, type Store } from './store';

export interface RealtimeDeps {
  connect: () => RealtimeConnection;
  /** This page load's client id (api/client.ts): the events it caused are not remote. */
  ownClientId: string;
  retryDelaysMs?: readonly number[];
  /** The hub refused as if the session ended: false when the API refuses too (its client sends the user to sign in). */
  confirmSession: () => Promise<boolean>;
}

// Who is on each screen of the open project (user ids, everyone including me), as the hub last said.
export type PresenceByScreen = ReadonlyMap<RouteId, readonly string[]>;

export interface Realtime extends Omit<RemoteChanges, 'handle' | 'reconnected'> {
  setProject: ProjectSubscription['setProject'];
  setScreen: ProjectSubscription['setScreen'];
  retryNow: ProjectSubscription['retryNow'];
  /** Invalidates every query of a project and returns how many; called after a reconnect. */
  setInvalidator(invalidate: (projectId: string) => number): void;
  connectionState: Store<ConnectionState>;
  presence: Store<PresenceByScreen>;
  dispose(): void;
}

const isRouteId = (value: unknown): value is RouteId =>
  typeof value === 'string' && (routeIds as readonly string[]).includes(value);

export function createRealtime(deps: RealtimeDeps): Realtime {
  const remote = createRemoteChanges(deps.ownClientId);
  const connectionState = createStore<ConnectionState>('connecting');
  const presence = createStore<PresenceByScreen>(new Map());
  // The sequence of the list kept for each screen. Lists from several server instances can arrive out of order.
  let sequences = new Map<RouteId, number>();
  let invalidate: (projectId: string) => number = () => 0;

  const subscription = createProjectSubscription({
    connect: deps.connect,
    retryDelaysMs: deps.retryDelaysMs,
    confirmSession: deps.confirmSession,
    onState: (state) => connectionState.set(state),
    onReset: () => {
      sequences = new Map();
      if (presence.get().size > 0) presence.set(new Map());
    },
    onReconnected: (projectId) =>
      remote.reconnected(projectId === undefined ? 0 : invalidate(projectId)),
    onEvent: (value) => {
      const event = value as ProjectEvent;
      // The screen id, user ids and sequence come from the wire: only a known screen, plain strings and a number are
      // kept. A list older than the one kept for its screen is dropped.
      if (
        event.type === 'presenceChanged' &&
        isRouteId(event.screenId) &&
        Array.isArray(event.userIds) &&
        typeof event.sequence === 'number'
      ) {
        if (event.sequence <= (sequences.get(event.screenId) ?? -Infinity)) return;
        sequences.set(event.screenId, event.sequence);
        const next = new Map(presence.get());
        next.set(
          event.screenId,
          event.userIds.filter((id) => typeof id === 'string'),
        );
        presence.set(next);
      }
      remote.handle(event);
    },
  });

  return {
    setProject: subscription.setProject,
    setScreen: subscription.setScreen,
    retryNow: subscription.retryNow,
    setInvalidator(fn) {
      invalidate = fn;
    },
    registerInvalidation: remote.registerInvalidation,
    onRemote: remote.onRemote,
    onReconnected: remote.onReconnected,
    connectionState,
    presence,
    dispose: subscription.dispose,
  };
}
