import type { KeyValueStore } from '@shiplean/core/platform';
import { inboxKeys } from '@shiplean/core/api/inbox';
import { createSyncStoragePersister } from '@tanstack/query-sync-storage-persister';
import type { PersistedClient, Persister } from '@tanstack/react-query-persist-client';
import type { Query } from '@tanstack/react-query';

// A kept Inbox older than this is dropped on the next start.
export const INBOX_MAX_AGE_MS = 7 * 24 * 60 * 60 * 1000;

const INBOX_ROOT = inboxKeys.inbox('')[0];

// Only the Inbox and Answered queries are kept on the phone (both keys start with the Inbox root).
export const isInboxQuery = (query: Pick<Query, 'queryKey' | 'state'>): boolean =>
  query.queryKey[0] === INBOX_ROOT && query.state.status === 'success';

// The sync persister over the kv store. The kv key is the session's now (null: signed out with no server, so nothing is
// kept). An empty cache removes the entry instead of writing one, so a save that lands after a clear leaves nothing.
export function createInboxPersister(store: KeyValueStore, currentKey: () => string | null): Persister {
  const inner = createSyncStoragePersister({
    storage: {
      getItem: (_k) => {
        const key = currentKey();
        return key === null ? null : store.getItem(key);
      },
      setItem: (_k, value) => {
        const key = currentKey();
        if (key !== null) store.setItem(key, value);
      },
      removeItem: (_k) => {
        const key = currentKey();
        if (key !== null) store.removeItem(key);
      },
    },
    throttleTime: 500,
  });
  return {
    ...inner,
    persistClient: (client: PersistedClient) =>
      client.clientState.queries.length === 0 ? inner.removeClient() : inner.persistClient(client),
  };
}

// buster: the app version, so a cache written by another version is dropped.
export function inboxPersistOptions(persister: Persister, buster: string) {
  return {
    persister,
    maxAge: INBOX_MAX_AGE_MS,
    buster,
    dehydrateOptions: { shouldDehydrateQuery: isInboxQuery },
  };
}
