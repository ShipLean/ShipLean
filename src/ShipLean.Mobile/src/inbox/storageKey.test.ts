import { describe, expect, it } from 'vitest';
import { inboxQueryStoreKey } from './storageKey';

describe('inboxQueryStoreKey', () => {
  it('keys the persisted Inbox by origin and email', () => {
    expect(inboxQueryStoreKey('https://shiplean.test', 'tom@example.com')).toBe(
      'shiplean.inbox.v1:https://shiplean.test:tom@example.com',
    );
    expect(inboxQueryStoreKey('https://shiplean.test', 'priya@example.com')).not.toBe(
      inboxQueryStoreKey('https://shiplean.test', 'tom@example.com'),
    );
    expect(inboxQueryStoreKey('https://other.test', 'tom@example.com')).not.toBe(
      inboxQueryStoreKey('https://shiplean.test', 'tom@example.com'),
    );
    expect(inboxQueryStoreKey(null, 'tom@example.com')).toBeNull();
  });
});
