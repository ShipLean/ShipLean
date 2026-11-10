import { memoryStore } from '@shiplean/core/platform';
import { describe, expect, it } from 'vitest';
import {
  addBatch,
  emptyOutbox,
  load,
  markSent,
  outboxKey,
  prune,
  releaseHeld,
  remove,
  serialise,
  update,
  visible,
  type Outbox,
} from './outbox';
import { scopeOf } from './scope';
import type { AppAction, PendingState } from './types';
import { UNDO, VOICE_ROW_MS } from './undo';

const T0 = Date.parse('2030-01-01T09:00:00.000Z');
const at = (ms: number) => new Date(T0 + ms);
const act = (itemId: string): AppAction => ({
  area: 'workItem',
  kind: 'setState',
  itemId,
  key: `SHIP-${itemId}`,
});
const one = (id: string) => [{ id, action: act(id) }];
const tap = { undo: UNDO.tap, origin: 'tap', allOrNothing: true } as const;
const none = { undo: UNDO.none, origin: 'tap', allOrNothing: true } as const;
const stateOf = (o: Outbox, id: string) => o.items.find((p) => p.id === id)?.state;

describe('outbox', () => {
  it('moves undo → queued → sending → sent and undo → removed', () => {
    let o = addBatch(emptyOutbox, 'b1', one('1'), tap, at(0), false);
    expect(o.items[0]).toMatchObject({
      state: 'undo',
      undoUntil: at(UNDO.tap.ms).toISOString(),
      createdAt: at(0).toISOString(),
    });

    o = releaseHeld(o, 'b1');
    expect(stateOf(o, '1')).toBe('queued');
    o = update(o, '1', { state: 'sending' });
    expect(stateOf(o, '1')).toBe('sending');
    o = markSent(o, '1', { createdId: 'c-1' }, at(1000));
    expect(o.items[0]).toMatchObject({ state: 'sent', receipt: { createdId: 'c-1' } });

    o = addBatch(o, 'b2', one('2'), tap, at(2000), false);
    expect(stateOf(o, '2')).toBe('undo');
    o = remove(o, ['2']);
    expect(o.items.map((p) => p.id)).toEqual(['1']);
    expect(Object.keys(o.batches)).toEqual(['b1']);
  });

  it('restarts hold items with a fresh window, keeps queued, turns sending into uncertain', () => {
    const states: [string, PendingState][] = [
      ['held', 'undo'],
      ['queued', 'queued'],
      ['sending', 'sending'],
      ['uncertain', 'uncertain'],
      ['failed', 'failed'],
      ['sent', 'sent'],
    ];
    let o = emptyOutbox;
    for (const [id, state] of states) {
      o = addBatch(o, id, one(id), state === 'undo' ? tap : none, at(0), false);
      if (state === 'sent') o = markSent(o, id, { createdId: 'c-9' }, at(1000));
      else if (state !== 'undo') o = update(o, id, { state });
    }

    const restartAt = 60_000;
    const back = load(serialise(o), at(restartAt));

    expect(back.items.map((p) => [p.id, p.state])).toEqual([
      ['held', 'undo'],
      ['queued', 'queued'],
      ['sending', 'uncertain'],
      ['uncertain', 'uncertain'],
      ['failed', 'failed'],
      ['sent', 'sent'],
    ]);
    expect(back.items[0].undoUntil).toBe(at(restartAt + UNDO.tap.ms).toISOString());
    expect(Object.values(back.batches).every((b) => !b.live)).toBe(true);
  });

  it('stores one key per scope and loads it back', () => {
    const tom = scopeOf({ origin: 'https://shiplean.test', email: 'tom@example.com' }) as string;
    const amy = scopeOf({ origin: 'https://shiplean.test', email: 'amy@example.com' }) as string;
    const tomElsewhere = scopeOf({ origin: 'https://other.test', email: 'tom@example.com' });
    expect(new Set([outboxKey(tom), outboxKey(amy), outboxKey(tomElsewhere as string)]).size).toBe(
      3,
    );

    const store = memoryStore();
    let o = addBatch(emptyOutbox, 'b1', one('1'), none, at(0), true);
    o = update(o, '1', { state: 'failed', error: 'Bad.', failure: 'validation' });
    store.setItem(outboxKey(tom), serialise(o) as string);

    expect(load(store.getItem(outboxKey(tom)), at(0)).items).toEqual(o.items);
    expect(load(store.getItem(outboxKey(amy)), at(0)).items).toEqual([]);
  });

  it('drops sent entries after 10 minutes', () => {
    let o = addBatch(emptyOutbox, 'b1', one('1'), none, at(0), false);
    o = markSent(o, '1', null, at(5000));

    expect(visible(o, at(5000 + VOICE_ROW_MS))).toHaveLength(1);
    expect(visible(o, at(5000 + VOICE_ROW_MS + 1))).toEqual([]);
    expect(prune(o, at(5000 + VOICE_ROW_MS)).items).toHaveLength(1);
    expect(prune(o, at(5000 + VOICE_ROW_MS + 1)).items).toEqual([]);
  });

  it('round-trips a sent entry with its receipt and drops it after 10 minutes', () => {
    let o = addBatch(emptyOutbox, 'b1', one('1'), none, at(0), false);
    o = markSent(o, '1', { createdId: 'c-1' }, at(5000));
    const raw = serialise(o);

    const back = load(raw, at(5000 + VOICE_ROW_MS));
    expect(back.items).toEqual(o.items);
    expect(back.items[0]).toMatchObject({ state: 'sent', receipt: { createdId: 'c-1' } });
    expect(back.sentAt).toEqual({ '1': at(5000).toISOString() });
    expect(Object.keys(back.batches)).toEqual(['b1']);

    const late = load(raw, at(5000 + VOICE_ROW_MS + 1));
    expect(late.items).toEqual([]);
    expect(late.sentAt).toEqual({});
  });

  it('refuses a value that is not an outbox', () => {
    expect(() => load('{not json', at(0))).toThrow();
    expect(() => load('{"items":5,"batches":{}}', at(0))).toThrow();
    expect(() => load('[]', at(0))).toThrow();
  });
});
