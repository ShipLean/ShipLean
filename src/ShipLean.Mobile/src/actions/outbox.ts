// The runner's state as a pure value: every Pending, how each batch was dispatched, and when each sent entry went.
// The clock is passed in; nothing here reads it. serialise/load are what the kv store keeps for one scope.
import type { AppAction, DispatchOptions, Pending, SendReceipt } from './types';
import { VOICE_ROW_MS } from './undo';

export interface BatchInfo {
  mode: 'hold' | 'after';
  allOrNothing: boolean;
  // Dispatched while online and not yet waiting for a connection: an allOrNothing batch stops at its first failure.
  // A batch that waited (offline, or loaded after a restart) sends each action on its own and carries on.
  live: boolean;
}

export interface Outbox {
  items: Pending[];
  batches: Record<string, BatchInfo>;
  // ISO 8601 UTC, per sent Pending id: sent entries stay in pending() for VOICE_ROW_MS. Never persisted.
  sentAt: Record<string, string>;
}

export const emptyOutbox: Outbox = { items: [], batches: {}, sentAt: {} };

// One kv key per scope (scope.ts).
export function outboxKey(scope: string): string {
  return `shiplean.outbox\n${scope}`;
}

const cleared = { error: null, failure: null, retryable: false } as const;

// hold with a window: each action waits in undo. Otherwise each is queued at once (after keeps its undo window).
export function addBatch(
  o: Outbox,
  batchId: string,
  actions: { id: string; action: AppAction }[],
  opts: DispatchOptions,
  now: Date,
  offline: boolean,
): Outbox {
  const held = opts.undo.mode === 'hold' && opts.undo.ms > 0;
  const undoUntil = opts.undo.ms > 0 ? new Date(now.getTime() + opts.undo.ms).toISOString() : null;
  const added: Pending[] = actions.map(({ id, action }) => ({
    id,
    batchId,
    action,
    origin: opts.origin,
    state: held ? 'undo' : 'queued',
    undoUntil,
    ...cleared,
    createdAt: now.toISOString(),
    undoOutcome: null,
    receipt: null,
  }));
  return {
    ...o,
    items: [...o.items, ...added],
    batches: {
      ...o.batches,
      [batchId]: { mode: opts.undo.mode, allOrNothing: opts.allOrNothing, live: !offline },
    },
  };
}

export function update(o: Outbox, id: string, patch: Partial<Pending>): Outbox {
  return { ...o, items: o.items.map((p) => (p.id === id ? { ...p, ...patch } : p)) };
}

// A hold batch's window ended: its actions are queued to send.
export function releaseHeld(o: Outbox, batchId: string): Outbox {
  return {
    ...o,
    items: o.items.map((p) =>
      p.batchId === batchId && p.state === 'undo' ? { ...p, state: 'queued' } : p,
    ),
  };
}

export function requeue(o: Outbox, id: string): Outbox {
  return update(o, id, { state: 'queued', ...cleared });
}

export function markSent(o: Outbox, id: string, receipt: SendReceipt | null, now: Date): Outbox {
  return {
    ...update(o, id, { state: 'sent', receipt, ...cleared }),
    sentAt: { ...o.sentAt, [id]: now.toISOString() },
  };
}

// The batches still waiting for a connection send each action on their own from now on.
export function notLive(o: Outbox): Outbox {
  const batches: Record<string, BatchInfo> = {};
  for (const [id, b] of Object.entries(o.batches)) batches[id] = { ...b, live: false };
  return { ...o, batches };
}

export function remove(o: Outbox, ids: string[]): Outbox {
  const gone = new Set(ids);
  const items = o.items.filter((p) => !gone.has(p.id));
  const sentAt = Object.fromEntries(Object.entries(o.sentAt).filter(([id]) => !gone.has(id)));
  return { items, batches: batchesOf(items, o.batches), sentAt };
}

function sentExpired(o: Outbox, p: Pending, now: Date): boolean {
  const at = o.sentAt[p.id];
  return p.state === 'sent' && at !== undefined && now.getTime() - Date.parse(at) > VOICE_ROW_MS;
}

// Drops sent entries older than VOICE_ROW_MS.
export function prune(o: Outbox, now: Date): Outbox {
  const expired = o.items.filter((p) => sentExpired(o, p, now)).map((p) => p.id);
  return expired.length === 0 ? o : remove(o, expired);
}

// What pending() shows: sent entries for VOICE_ROW_MS, and a hold whose window has ended as queued.
export function visible(o: Outbox, now: Date): Pending[] {
  const at = now.getTime();
  return o.items
    .filter((p) => !sentExpired(o, p, now))
    .map((p) =>
      p.state === 'undo' && p.undoUntil !== null && Date.parse(p.undoUntil) <= at
        ? { ...p, state: 'queued' }
        : p,
    );
}

function batchesOf(items: Pending[], batches: Record<string, BatchInfo>) {
  const ids = new Set(items.map((p) => p.batchId));
  return Object.fromEntries(Object.entries(batches).filter(([id]) => ids.has(id)));
}

interface Stored {
  items: Pending[];
  batches: Record<string, BatchInfo>;
}

// Everything but sent; null when nothing is left to keep.
export function serialise(o: Outbox): string | null {
  const items = o.items.filter((p) => p.state !== 'sent');
  if (items.length === 0) return null;
  const stored: Stored = { items, batches: batchesOf(items, o.batches) };
  return JSON.stringify(stored);
}

// After a restart: a hold gets a fresh window from now, as long as the one it had; queued and failed stay; a send
// that was under way may or may not have reached the server, so it is uncertain. Every batch has waited.
export function load(raw: string | null, now: Date): Outbox {
  if (raw === null) return emptyOutbox;
  const stored = JSON.parse(raw) as Stored;
  const items = stored.items.map((p): Pending => {
    if (p.state === 'undo' && p.undoUntil !== null) {
      const length = Date.parse(p.undoUntil) - Date.parse(p.createdAt);
      return { ...p, undoUntil: new Date(now.getTime() + length).toISOString() };
    }
    if (p.state === 'sending') return { ...p, state: 'uncertain' };
    return p;
  });
  return notLive({ items, batches: stored.batches, sentAt: {} });
}
