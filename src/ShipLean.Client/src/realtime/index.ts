// The real-time client: one hub connection per tab (module-level state, so no provider), joined to the open project and
// screen. The server side and the wire format are in docs/events.md ("The hub").
//
//   useProjectRealtime(projectKey)        Shell calls it. Resolves the key (['projects', 'by-key', key]), joins the
//                                         project, leaves on change or unmount. After a reconnect it rejoins, invalidates
//                                         every ['projects', projectId, ...] query and tells onReconnected listeners.
//   useScreenPresence(routeId)            Each page calls it with its route id. Joins that screen while mounted (one
//                                         screen per project; the previous one is left). No-op for install-level routes.
//   registerInvalidation(type, handler)   Calls handler(event) for each remote event of that type, from every screen of
//                                         the open project: filter on the event's ids. Returns unregister.
//   onRemote(handler)                     Every remote event. "Remote" skips events this tab caused (clientId equal to
//                                         api/client.ts's clientId), except importProgress and presenceChanged, which
//                                         always pass. Returns unregister.
//   onReconnected(handler)                handler(refetched) after each reconnect (also the first connect that succeeds after
//                                         a failed attempt). refetched is how many cached queries under the project were
//                                         refreshed, not how many things changed: word any UI text as a refresh, never
//                                         "n changes". Handlers may run before the refetch completes. Returns unregister.
//   useConnectionState()                  'connecting' | 'live' | 'reconnecting' | 'offline'.
//   usePresence(routeId)                  User ids on that screen other than me.
//   LiveDot, PresenceStack                The top bar's pieces; TopBarRealtime (shell/slots) assembles them.
//
// Nothing here needs a hub: a hub that never connects leaves the state at 'connecting' or 'offline', the hooks keep
// working and the connection keeps retrying with back-off. Strings in events are untrusted text.
import { realtime } from './realtime';

export { useProjectRealtime } from './useProjectRealtime';
export { useScreenPresence } from './useScreenPresence';
export { useConnectionState } from './useConnectionState';
export { usePresence } from './usePresence';
export { LiveDot } from './LiveDot';
export { PresenceStack } from './PresenceStack';
export type { ConnectionState } from './connectionState';

export const registerInvalidation = realtime.registerInvalidation;
export const onRemote = realtime.onRemote;
export const onReconnected = realtime.onReconnected;
