import { inboxKeys } from '@shiplean/core/api/inbox';
import { projectsKey } from '@shiplean/core/api/projects';
import { QueryClient } from '@tanstack/react-query';
import {
  persistQueryClientRestore,
  persistQueryClientSave,
  type PersistedClient,
} from '@tanstack/react-query-persist-client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  createInboxPersister,
  inboxPersistOptions,
  INBOX_MAX_AGE_MS,
  keepInboxQueries,
} from './persist';

const KEY = 'shiplean.inbox.v1:https://shiplean.test:me@example.com';

function memoryStore() {
  const values = new Map<string, string>();
  return {
    values,
    getItem: (k: string) => values.get(k) ?? null,
    setItem: (k: string, v: string) => void values.set(k, v),
    removeItem: (k: string) => void values.delete(k),
  };
}

function setup(buster = '1.0.0') {
  const store = memoryStore();
  const persister = createInboxPersister(store, () => KEY);
  const options = inboxPersistOptions(persister, buster);
  return { store, persister, options };
}

const keptQueries = (store: ReturnType<typeof memoryStore>): unknown[] => {
  const raw = store.values.get(KEY);
  return raw === undefined
    ? []
    : (JSON.parse(raw) as PersistedClient).clientState.queries.map((q) => q.queryKey);
};

describe('persisted queries', () => {
  beforeEach(() => vi.useFakeTimers({ toFake: ['Date', 'setTimeout', 'clearTimeout'] }));
  afterEach(() => vi.useRealTimers());

  it('keeps only the Inbox, Answered and projects list queries', async () => {
    const { store, options } = setup();
    const client = new QueryClient();
    client.setQueryData(inboxKeys.inbox('p1'), { items: [] });
    client.setQueryData(inboxKeys.answered('p1'), { rows: [] });
    client.setQueryData(['projects'], [{ key: 'SHIP' }]);
    client.setQueryData(['work-item', 'SHIP-1'], { title: 'x' });

    await persistQueryClientSave({ queryClient: client, ...options });
    await vi.advanceTimersByTimeAsync(1000);

    expect(keptQueries(store)).toEqual([
      inboxKeys.inbox('p1'),
      inboxKeys.answered('p1'),
      projectsKey,
    ]);
  });

  it('keeps nothing, and removes the entry, when no kept query is left', async () => {
    const { store, options } = setup();
    store.values.set(KEY, 'old');
    const client = new QueryClient();
    client.setQueryData(['work-item', 'SHIP-1'], { title: 'x' });
    await persistQueryClientSave({ queryClient: client, ...options });
    await vi.advanceTimersByTimeAsync(1000);
    expect(store.values.has(KEY)).toBe(false);
  });

  it('keeps nothing while there is no key', async () => {
    const store = memoryStore();
    const persister = createInboxPersister(store, () => null);
    const client = new QueryClient();
    client.setQueryData(inboxKeys.inbox('p1'), { items: [] });
    await persistQueryClientSave({
      queryClient: client,
      ...inboxPersistOptions(persister, '1.0.0'),
    });
    await vi.advanceTimersByTimeAsync(1000);
    expect(store.values.size).toBe(0);
  });

  it('restores what was kept', async () => {
    const { options } = setup();
    const first = new QueryClient();
    first.setQueryData(inboxKeys.inbox('p1'), { items: ['a'] });
    await persistQueryClientSave({ queryClient: first, ...options });
    await vi.advanceTimersByTimeAsync(1000);

    const restarted = new QueryClient();
    await persistQueryClientRestore({ queryClient: restarted, ...options });
    expect(restarted.getQueryData(inboxKeys.inbox('p1'))).toEqual({ items: ['a'] });
  });

  it('restores the projects list, and keeps it in the cache with nothing observing it', async () => {
    const { options } = setup();
    const first = new QueryClient();
    first.setQueryData(projectsKey, [{ key: 'SHIP' }]);
    await persistQueryClientSave({ queryClient: first, ...options });
    await vi.advanceTimersByTimeAsync(1000);

    const restarted = new QueryClient();
    keepInboxQueries(restarted);
    await persistQueryClientRestore({ queryClient: restarted, ...options });
    expect(restarted.getQueryData(projectsKey)).toEqual([{ key: 'SHIP' }]);
    await vi.advanceTimersByTimeAsync(10 * 60 * 1000);
    expect(restarted.getQueryData(projectsKey)).toEqual([{ key: 'SHIP' }]);
  });

  it('does not keep a projects query with another key shape', async () => {
    const { store, options } = setup();
    const client = new QueryClient();
    client.setQueryData([...projectsKey, 'SHIP'], { key: 'SHIP' });
    await persistQueryClientSave({ queryClient: client, ...options });
    await vi.advanceTimersByTimeAsync(1000);
    expect(store.values.has(KEY)).toBe(false);
  });

  // Sign-out (clearInboxStorage) removes the one kv entry, and the projects list lives in it.
  it('clears the projects list with the Inbox on sign-out, even when a later save lands', async () => {
    const { store, options } = setup();
    const client = new QueryClient();
    client.setQueryData(inboxKeys.inbox('p1'), { items: [] });
    client.setQueryData(projectsKey, [{ key: 'SHIP' }]);
    await persistQueryClientSave({ queryClient: client, ...options });
    await vi.advanceTimersByTimeAsync(1000);
    expect(keptQueries(store)).toContainEqual(projectsKey);

    store.removeItem(KEY);
    client.clear();
    await persistQueryClientSave({ queryClient: client, ...options });
    await vi.advanceTimersByTimeAsync(1000);

    const restarted = new QueryClient();
    await persistQueryClientRestore({ queryClient: restarted, ...options });
    expect(restarted.getQueryData(projectsKey)).toBeUndefined();
    expect(store.values.has(KEY)).toBe(false);
  });

  it('drops a cache written by another app version', async () => {
    const { store, options } = setup('1.0.0');
    const first = new QueryClient();
    first.setQueryData(inboxKeys.inbox('p1'), { items: ['a'] });
    await persistQueryClientSave({ queryClient: first, ...options });
    await vi.advanceTimersByTimeAsync(1000);

    const restarted = new QueryClient();
    await persistQueryClientRestore({
      queryClient: restarted,
      ...inboxPersistOptions(options.persister, '1.1.0'),
    });
    expect(restarted.getQueryData(inboxKeys.inbox('p1'))).toBeUndefined();
    expect(store.values.has(KEY)).toBe(false);
  });

  it('drops a cache older than 7 days', async () => {
    const { store, options } = setup();
    const first = new QueryClient();
    first.setQueryData(inboxKeys.inbox('p1'), { items: ['a'] });
    await persistQueryClientSave({ queryClient: first, ...options });
    await vi.advanceTimersByTimeAsync(1000);

    vi.setSystemTime(Date.now() + INBOX_MAX_AGE_MS + 60_000);
    const restarted = new QueryClient();
    await persistQueryClientRestore({ queryClient: restarted, ...options });
    expect(restarted.getQueryData(inboxKeys.inbox('p1'))).toBeUndefined();
    expect(store.values.has(KEY)).toBe(false);
  });

  it('restores a cache 6 days old', async () => {
    const { options } = setup();
    const first = new QueryClient();
    first.setQueryData(inboxKeys.inbox('p1'), { items: ['a'] });
    await persistQueryClientSave({ queryClient: first, ...options });
    await vi.advanceTimersByTimeAsync(1000);

    vi.setSystemTime(Date.now() + 6 * 24 * 60 * 60 * 1000);
    const restarted = new QueryClient();
    await persistQueryClientRestore({ queryClient: restarted, ...options });
    expect(restarted.getQueryData(inboxKeys.inbox('p1'))).toEqual({ items: ['a'] });
  });

  it('keeps a restored Inbox and Answered in the cache with nothing observing them', async () => {
    const { options } = setup();
    const first = new QueryClient();
    first.setQueryData(inboxKeys.inbox('p1'), { items: ['a'] });
    first.setQueryData(inboxKeys.answered('p1'), { rows: ['b'] });
    await persistQueryClientSave({ queryClient: first, ...options });
    await vi.advanceTimersByTimeAsync(1000);

    // gcTime is Infinity under node, so state the app's default (5 minutes) the way React Native sees it.
    const restarted = new QueryClient({ defaultOptions: { queries: { gcTime: 5 * 60 * 1000 } } });
    keepInboxQueries(restarted);
    await persistQueryClientRestore({ queryClient: restarted, ...options });
    await vi.advanceTimersByTimeAsync(6 * 60 * 1000);

    expect(restarted.getQueryData(inboxKeys.inbox('p1'))).toEqual({ items: ['a'] });
    expect(restarted.getQueryData(inboxKeys.answered('p1'))).toEqual({ rows: ['b'] });
  });
});
