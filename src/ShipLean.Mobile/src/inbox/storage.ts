import { platform } from '@shiplean/core/platform';
import { session } from '../session/session';
import { inboxQueryStoreKey } from './storageKey';

// The persisted Inbox's key for the session now, or null when there is none.
export function currentInboxQueryStoreKey(): string | null {
  const { origin, email } = session.get();
  return inboxQueryStoreKey(origin, email);
}

// Removes the persisted Inbox of the session now. The outbox is the runner's to clear.
export function clearInboxStorage(): void {
  const key = currentInboxQueryStoreKey();
  if (key !== null) platform().persistentStore().removeItem(key);
}
