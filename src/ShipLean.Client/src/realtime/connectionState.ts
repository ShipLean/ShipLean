// connecting: the first attempt is under way. live: connected. reconnecting: a connection that was up has dropped.
// offline: the first connection could not be made (it keeps retrying).
export type ConnectionState = 'connecting' | 'live' | 'reconnecting' | 'offline';
