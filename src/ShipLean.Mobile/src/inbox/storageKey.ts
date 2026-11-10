// Where the persisted Inbox query is kept, per server and person. Pure: the session isn't read here.
// Null when there is no server (signed out with nothing stored): nothing is persisted then.
export function inboxQueryStoreKey(origin: string | null, email: string): string | null {
  return origin === null ? null : `shiplean.inbox.v1:${origin}:${email}`;
}
