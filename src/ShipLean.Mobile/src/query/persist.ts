import type { KeyValueStore } from '@shiplean/core/platform';
import { inboxKeys } from '@shiplean/core/api/inbox';
import { projectsKey } from '@shiplean/core/api/projects';
import { createSyncStoragePersister } from '@tanstack/query-sync-storage-persister';
import type { Persister } from '@tanstack/react-query-persist-client';
import type { Query, QueryClient } from '@tanstack/react-query';

// A kept Inbox older than this is dropped on the next start.
export const INBOX_MAX_AGE_MS = 7 * 24 * 60 * 60 * 1000;

const INBOX_ROOT = inboxKeys.inbox('')[0];

// Restored queries have no observer until their screen opens, and the default gcTime (5 minutes) would drop them from
// the cache long before maxAge. Keep the kept queries as long as the persister does.
export function keepInboxQueries(client: QueryClient): void {
  client.setQueryDefaults([INBOX_ROOT], { gcTime: INBOX_MAX_AGE_MS });
  client.setQueryDefaults(projectsKey, { gcTime: INBOX_MAX_AGE_MS });
}

// The Inbox and Answered queries (both keys start with the Inbox root).
export const isInboxQuery = (query: Pick<Query, 'queryKey' | 'state'>): boolean =>
  query.queryKey[0] === INBOX_ROOT && query.state.status === 'success';

// The projects list is kept too: useProject reads the open project from it, so a cold start with no connection (where
// react-query pauses the list) can still open the kept Inbox. It lives in the same kv entry as the Inbox, so sign-out
// (clearInboxStorage) and a lost session clear it with them.
export const isKeptQuery = (query: Pick<Query, 'queryKey' | 'state'>): boolean =>
  isInboxQuery(query) ||
  (query.queryKey.length === projectsKey.length &&
    query.queryKey[0] === projectsKey[0] &&
    query.state.status === 'success');

// The sync persister over the kv store. The kv key is the session's now (null: signed out with no server, so nothing is
// kept). An empty cache serialises to '' and removes the entry instead of writing one, so a save that lands after a
// clear (the persister throttles, and always writes the latest state) leaves nothing behind.
export function createInboxPersister(
  store: KeyValueStore,
  currentKey: () => string | null,
): Persister {
  return createSyncStoragePersister({
    storage: {
      getItem: () => {
        const key = currentKey();
        return key === null ? null : store.getItem(key);
      },
      setItem: (_key, value) => {
        const key = currentKey();
        if (key === null) return;
        if (value === '') store.removeItem(key);
        else store.setItem(key, value);
      },
      removeItem: () => {
        const key = currentKey();
        if (key !== null) store.removeItem(key);
      },
    },
    serialize: (client) => (client.clientState.queries.length === 0 ? '' : JSON.stringify(client)),
    throttleTime: 500,
  });
}

// buster: the app version, so a cache written by another version is dropped.
export function inboxPersistOptions(persister: Persister, buster: string) {
  return {
    persister,
    maxAge: INBOX_MAX_AGE_MS,
    buster,
    dehydrateOptions: { shouldDehydrateQuery: isKeptQuery },
  };
}
