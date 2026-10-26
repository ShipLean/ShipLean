import {
  HttpTransportType,
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';

// The server side is ProjectHub (docs/events.md, "The hub"): JoinProject / LeaveProject / JoinScreen / LeaveScreen, and
// the client method "projectEvent" carrying one ProjectEvent.
export const PROJECT_HUB_PATH = '/hubs/project';
export const EVENT_METHOD = 'projectEvent';

// The slice of HubConnection the subscription uses, so a test passes a fake.
export type RealtimeConnection = Pick<
  HubConnection,
  'start' | 'stop' | 'invoke' | 'on' | 'onclose'
>;

// Seconds-scale back-off for the first connect and for every reconnect. The last delay repeats.
export const RETRY_DELAYS_MS: readonly number[] = [0, 2000, 5000, 10000, 30000];

// WebSockets only: the hub offers no other transport. Reconnecting is the subscription's job (it rejoins the project
// and screen after each start), so SignalR's own automatic reconnect is off.
export function createProjectConnection(): RealtimeConnection {
  return new HubConnectionBuilder()
    .withUrl(PROJECT_HUB_PATH, { transport: HttpTransportType.WebSockets })
    .configureLogging(LogLevel.None)
    .build();
}
