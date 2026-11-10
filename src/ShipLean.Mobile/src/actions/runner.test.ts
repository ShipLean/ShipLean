// What the runner does beyond runnerRules.test.ts (which pins allOrNothing, 409, after + undo, irreversible, the failed
// voice batch's reversal, the offline queue, reconcile, the 10 minutes, scope and the restart window): the hold rule,
// the failed voice batch's wording, the error copy, the receipt, and the window ending.
import { ApiRequestError } from '@shiplean/core/api/client';
import { memoryStore, type KeyValueStore } from '@shiplean/core/platform';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { outboxKey } from './outbox';
import { createActionRunner } from './runner';
import { scopeOf } from './scope';
import type { AppAction, Executor, Pending, SendReceipt } from './types';
import { UNDO } from './undo';

const T0 = Date.parse('2030-01-01T09:00:00.000Z');
const SCOPE = scopeOf({ origin: 'https://shiplean.test', email: 'tom@example.com' }) as string;

const act = (kind: string, itemId: string): AppAction => ({
  area: 'workItem',
  kind,
  itemId,
  key: `SHIP-${itemId}`,
});

// Records every send and undo. approve is irreversible; the rest are reversible.
function recorder() {
  const calls: string[] = [];
  const sendFails = new Map<string, unknown>();
  const undoFails = new Set<string>();
  const receipts = new Map<string, SendReceipt>();
  const undoReceipts: (SendReceipt | null | undefined)[] = [];
  const executor: Executor<AppAction> = {
    area: 'workItem',
    send: async (a) => {
      calls.push(`send ${a.itemId}`);
      const failure = sendFails.get(a.itemId as string);
      if (failure !== undefined) throw failure;
      return receipts.get(a.itemId as string);
    },
    describe: (a) => ({ id: `${a.itemId}`, icon: 'state', text: a.kind, removable: true }),
    reversible: (kind) => kind !== 'approve',
    undo: async (a, _api, receipt) => {
      calls.push(`undo ${a.itemId}`);
      undoReceipts.push(receipt);
      if (undoFails.has(a.itemId as string)) throw new TypeError('Network request failed');
    },
  };
  return { calls, sendFails, undoFails, receipts, undoReceipts, executor };
}

const refusal = (
  status: number,
  code: 'validation' | 'not_found' | 'internal_error' | 'conflict',
) => new ApiRequestError(status === 400 ? 'Title is too long.' : 'Refused.', status, code);

// A started runner over its own connection: goOnline() fires what the runner gave onOnline.
function build(
  rec: ReturnType<typeof recorder>,
  {
    store = memoryStore(),
    start = true,
    register = true,
  }: { store?: KeyValueStore; start?: boolean; register?: boolean } = {},
) {
  const listeners = new Set<() => void>();
  const runner = createActionRunner({
    store,
    scope: SCOPE,
    isOffline: () => false,
    onOnline: (cb) => {
      listeners.add(cb);
      return () => listeners.delete(cb);
    },
    now: () => new Date(),
  });
  if (start) runner.start();
  if (register) runner.register(rec.executor);
  return {
    runner,
    store,
    listeners,
    goOnline: () => {
      for (const cb of [...listeners]) cb();
    },
  };
}

function runnerWith(rec: ReturnType<typeof recorder>) {
  return build(rec).runner;
}

const flush = () => vi.advanceTimersByTimeAsync(0);
const byItem = (runner: ReturnType<typeof runnerWith>, itemId: string) =>
  runner.pending().find((p) => p.action.itemId === itemId);

beforeEach(() => {
  vi.useFakeTimers({ now: T0 });
});
afterEach(() => {
  vi.useRealTimers();
});

describe('ActionRunner', () => {
  it('sends nothing when a hold batch is undone inside the window', async () => {
    const rec = recorder();
    const runner = runnerWith(rec);

    const batchId = runner.dispatch([act('setState', '1'), act('assign', '2')], {
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
    await vi.advanceTimersByTimeAsync(UNDO.tap.ms - 1);
    const outcomes = await runner.undo(batchId);
    await vi.advanceTimersByTimeAsync(UNDO.tap.ms * 2);

    expect(outcomes.map((o) => o.outcome)).toEqual(['undone', 'undone']);
    expect(rec.calls).toEqual([]);
    expect(runner.pending()).toEqual([]);
  });

  it('sends a hold batch once its window ends, and not before', async () => {
    const rec = recorder();
    const runner = runnerWith(rec);

    runner.dispatch([act('setState', '1')], { undo: UNDO.tap, origin: 'tap', allOrNothing: true });
    await vi.advanceTimersByTimeAsync(UNDO.tap.ms - 1);
    expect(rec.calls).toEqual([]);
    expect(byItem(runner, '1')?.state).toBe('undo');

    await vi.advanceTimersByTimeAsync(1);
    expect(rec.calls).toEqual(['send 1']);
    expect(byItem(runner, '1')?.state).toBe('sent');
  });

  it('tells subscribers when an undo window ends', async () => {
    const rec = recorder();
    const runner = runnerWith(rec);
    runner.dispatch([act('setState', '1')], {
      undo: UNDO.typed,
      origin: 'typed',
      allOrNothing: true,
    });
    await flush();
    expect(byItem(runner, '1')?.state).toBe('sent');
    const heard = vi.fn();
    runner.subscribe(heard);

    await vi.advanceTimersByTimeAsync(UNDO.typed.ms - 1);
    expect(heard).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(heard).toHaveBeenCalled();
  });

  it('reverses the reversible sent part of a failed voice batch and says Nothing changed only when nothing stayed', async () => {
    const voice = { undo: UNDO.voice, origin: 'voice', allOrNothing: true } as const;

    // Every sent action reversed: Nothing changed.
    let rec = recorder();
    let runner = runnerWith(rec);
    rec.sendFails.set('2', refusal(400, 'validation'));
    runner.dispatch([act('setState', '1'), act('setState', '2'), act('setState', '3')], voice);
    await flush();
    expect(rec.calls).toEqual(['send 1', 'send 2', 'undo 1']);
    expect(byItem(runner, '1')?.undoOutcome).toBe('undone');
    expect(byItem(runner, '2')?.error).toBe('Nothing changed. Title is too long.');
    expect(byItem(runner, '3')).toMatchObject({ state: 'failed', undoOutcome: null });

    // Nothing sent at all: Nothing changed.
    rec = recorder();
    runner = runnerWith(rec);
    rec.sendFails.set('1', refusal(400, 'validation'));
    runner.dispatch([act('setState', '1'), act('setState', '2')], voice);
    await flush();
    expect(rec.calls).toEqual(['send 1']);
    expect(byItem(runner, '1')?.error).toBe('Nothing changed. Title is too long.');

    // An irreversible action went first and stayed: Partly applied.
    rec = recorder();
    runner = runnerWith(rec);
    rec.sendFails.set('3', refusal(400, 'validation'));
    runner.dispatch([act('approve', '1'), act('setState', '2'), act('setState', '3')], voice);
    await flush();
    expect(rec.calls).toEqual(['send 1', 'send 2', 'send 3', 'undo 2']);
    expect(byItem(runner, '1')?.undoOutcome).toBe('irreversible');
    expect(byItem(runner, '2')?.undoOutcome).toBe('undone');
    expect(byItem(runner, '3')?.error).toBe('Partly applied. Title is too long.');

    // A reverse that couldn't reach the server: Partly applied.
    rec = recorder();
    runner = runnerWith(rec);
    rec.sendFails.set('2', refusal(400, 'validation'));
    rec.undoFails.add('1');
    runner.dispatch([act('setState', '1'), act('setState', '2')], voice);
    await flush();
    expect(byItem(runner, '1')?.undoOutcome).toBe('failed');
    expect(byItem(runner, '2')?.error).toBe('Partly applied. Title is too long.');
  });

  it('maps 404, 400, 500 and no response to the error copy, failure and retryable flag', async () => {
    const rec = recorder();
    const runner = runnerWith(rec);
    rec.sendFails.set('1', refusal(404, 'not_found'));
    rec.sendFails.set('2', refusal(400, 'validation'));
    rec.sendFails.set('3', refusal(500, 'internal_error'));
    rec.sendFails.set('4', new TypeError('Network request failed'));
    rec.sendFails.set('5', refusal(409, 'conflict'));
    for (const id of ['1', '2', '3', '4', '5'])
      runner.dispatch([act('setState', id)], {
        undo: UNDO.none,
        origin: 'tap',
        allOrNothing: true,
      });
    await flush();

    const shown = (id: string) => {
      const p = byItem(runner, id);
      return [p?.state, p?.error, p?.failure, p?.retryable];
    };
    expect(shown('1')).toEqual(['failed', "SHIP-1 isn't there any more.", 'notFound', false]);
    expect(shown('2')).toEqual(['failed', 'Title is too long.', 'validation', false]);
    expect(shown('3')).toEqual(['failed', "Didn't send (500).", 'server', true]);
    expect(shown('4')).toEqual(['uncertain', 'No answer from ShipLean.', null, true]);
    expect(shown('5')).toEqual([
      'failed',
      'Changed on the server. Open SHIP-5 to check.',
      'conflict',
      false,
    ]);
  });

  it('tries a failed undo of a sent action again and leaves an undone one alone', async () => {
    const rec = recorder();
    const runner = runnerWith(rec);
    const batchId = runner.dispatch([act('setState', '1'), act('setState', '2')], {
      undo: UNDO.typed,
      origin: 'typed',
      allOrNothing: false,
    });
    await flush();
    rec.undoFails.add('1');

    expect((await runner.undo(batchId)).map((o) => o.outcome)).toEqual(['undone', 'failed']);
    rec.undoFails.delete('1');
    expect((await runner.undo(batchId)).map((o) => o.outcome)).toEqual(['undone', 'undone']);

    expect(rec.calls).toEqual(['send 1', 'send 2', 'undo 2', 'undo 1', 'undo 1']);
    expect(byItem(runner, '1')?.undoOutcome).toBe('undone');
  });

  it('keeps what send returned and gives it to undo', async () => {
    const rec = recorder();
    const runner = runnerWith(rec);
    rec.receipts.set('1', { createdId: 'comment-7' });

    const batchId = runner.dispatch([act('addNote', '1'), act('setState', '2')], {
      undo: UNDO.typed,
      origin: 'typed',
      allOrNothing: true,
    });
    await flush();
    expect(byItem(runner, '1')?.receipt).toEqual({ createdId: 'comment-7' });
    expect(byItem(runner, '2')?.receipt).toBeNull();

    await runner.undo(batchId);

    expect(rec.calls).toEqual(['send 1', 'send 2', 'undo 2', 'undo 1']);
    expect(rec.undoReceipts).toEqual([null, { createdId: 'comment-7' }]);
  });

  it('keeps a sent action and its receipt over a restart, so undo still reaches the server', async () => {
    const rec = recorder();
    const store = memoryStore();
    const first = build(rec, { store });
    rec.receipts.set('1', { createdId: 'comment-7' });
    const batchId = first.runner.dispatch([act('addNote', '1')], {
      undo: UNDO.typed,
      origin: 'typed',
      allOrNothing: true,
    });
    await flush();
    first.runner.stop();

    const rec2 = recorder();
    const second = build(rec2, { store });
    const outcomes = await second.runner.undo(batchId);

    expect(outcomes).toEqual([{ id: expect.any(String), outcome: 'undone' }]);
    expect(rec2.calls).toEqual(['undo 1']);
    expect(rec2.undoReceipts).toEqual([{ createdId: 'comment-7' }]);
  });

  it('answers failed, not nothing, for a sent action whose undo needs a receipt that is gone', async () => {
    const rec = recorder();
    const store = memoryStore();
    const first = build(rec, { store });
    const batchId = first.runner.dispatch([act('addNote', '1')], {
      undo: UNDO.typed,
      origin: 'typed',
      allOrNothing: true,
    });
    await flush();
    first.runner.stop();
    const raw = JSON.parse(store.getItem(outboxKey(SCOPE)) as string);
    raw.items[0].receipt = null;
    store.setItem(outboxKey(SCOPE), JSON.stringify(raw));

    const rec2 = recorder();
    rec2.executor.undo = async (_a, _api, receipt) => {
      if (receipt == null) throw new Error('needs the id of what was created');
    };
    const second = build(rec2, { store });
    const outcomes = await second.runner.undo(batchId);

    expect(outcomes.map((o) => o.outcome)).toEqual(['failed']);
  });

  it('sends nothing and listens for nothing until start()', async () => {
    const rec = recorder();
    const idle = build(rec, { start: false });

    idle.runner.dispatch([act('setState', '1')], {
      undo: UNDO.none,
      origin: 'tap',
      allOrNothing: true,
    });
    idle.goOnline();
    await vi.advanceTimersByTimeAsync(UNDO.tap.ms * 2);

    expect(idle.listeners.size).toBe(0);
    expect(rec.calls).toEqual([]);

    idle.runner.start();
    await flush();
    expect(idle.listeners.size).toBe(1);
    expect(rec.calls).toEqual(['send 1']);
  });

  it('keeps an uncertain action uncertain when reconcile gives no verdict for it', async () => {
    const rec = recorder();
    const { runner, goOnline } = build(rec);
    rec.sendFails.set('1', new TypeError('Network request failed'));
    rec.executor.reconcile = async () => [];
    runner.dispatch([act('setState', '1')], { undo: UNDO.none, origin: 'tap', allOrNothing: true });
    await flush();
    rec.sendFails.clear();

    goOnline();
    await flush();

    expect(byItem(runner, '1')?.state).toBe('uncertain');
    expect(rec.calls).toEqual(['send 1']);
  });

  it('treats only a fetch TypeError or AbortError as no answer; any other error is a server failure, not retryable', async () => {
    const rec = recorder();
    const runner = runnerWith(rec);
    rec.sendFails.set('1', new Error('boom'));
    rec.sendFails.set('2', new SyntaxError('Unexpected token < in JSON'));
    rec.sendFails.set(
      '3',
      Object.assign(new Error('The operation was aborted.'), { name: 'AbortError' }),
    );
    rec.sendFails.set('4', new TypeError('Network request failed'));
    for (const id of ['1', '2', '3', '4'])
      runner.dispatch([act('setState', id)], {
        undo: UNDO.none,
        origin: 'tap',
        allOrNothing: false,
      });
    await flush();

    const shown = (id: string) => {
      const p = byItem(runner, id);
      return [p?.state, p?.failure, p?.retryable];
    };
    expect(shown('1')).toEqual(['failed', 'server', false]);
    expect(shown('2')).toEqual(['failed', 'server', false]);
    expect(shown('3')).toEqual(['uncertain', null, true]);
    expect(shown('4')).toEqual(['uncertain', null, true]);
  });

  it('stops a live all-or-nothing batch at an executor error, and reverses a voice batch for it', async () => {
    let rec = recorder();
    let runner = runnerWith(rec);
    rec.sendFails.set('2', new Error('boom'));
    runner.dispatch([act('setState', '1'), act('setState', '2'), act('setState', '3')], {
      undo: UNDO.typed,
      origin: 'typed',
      allOrNothing: true,
    });
    await flush();
    expect(rec.calls).toEqual(['send 1', 'send 2']);
    expect(byItem(runner, '3')).toMatchObject({ state: 'failed' });

    rec = recorder();
    runner = runnerWith(rec);
    rec.sendFails.set('2', new Error('boom'));
    runner.dispatch([act('setState', '1'), act('setState', '2'), act('setState', '3')], {
      undo: UNDO.voice,
      origin: 'voice',
      allOrNothing: true,
    });
    await flush();
    expect(rec.calls).toEqual(['send 1', 'send 2', 'undo 1']);
    expect(byItem(runner, '2')?.error).toBe("Nothing changed. Didn't send.");
  });

  it('never resends an action the user undid while it was sending or uncertain, even on retry', async () => {
    const rec = recorder();
    const { runner, goOnline } = build(rec);
    let answer: (() => void) | undefined;
    rec.executor.send = (a) => {
      rec.calls.push(`send ${a.itemId}`);
      return new Promise((_, reject) => {
        answer = () => reject(new TypeError('Network request failed'));
      });
    };
    rec.executor.reconcile = async (items) => items.map(() => 'notSent' as const);
    const batchId = runner.dispatch([act('setState', '1')], {
      undo: UNDO.typed,
      origin: 'typed',
      allOrNothing: false,
    });
    await flush();
    expect(byItem(runner, '1')?.state).toBe('sending');

    expect(await runner.undo(batchId)).toEqual([
      { id: byItem(runner, '1')?.id, outcome: 'failed' },
    ]);
    answer?.();
    await flush();
    const id = byItem(runner, '1')?.id as string;
    runner.retry(id);
    goOnline();
    await flush();
    expect(rec.calls).toEqual(['send 1']);
  });

  it('holds a later action of a batch behind an earlier queued one whose area has no executor yet', async () => {
    const rec = recorder();
    const { runner } = build(rec);
    const retro: Executor<AppAction> = { ...rec.executor, area: 'retro' };

    runner.dispatch([{ ...act('addPoint', '1'), area: 'retro' }, act('setState', '2')], {
      undo: UNDO.none,
      origin: 'tap',
      allOrNothing: false,
    });
    await flush();
    expect(rec.calls).toEqual([]);

    runner.register(retro);
    await flush();
    expect(rec.calls).toEqual(['send 1', 'send 2']);
  });

  it('reconciles an uncertain send on online, on register and on retry, not at once', async () => {
    const rec = recorder();
    const store = memoryStore();
    const { runner, goOnline } = build(rec, { store });
    const reconciled: string[][] = [];
    rec.executor.reconcile = async (items) => {
      reconciled.push(items.map((p) => p.id));
      return items.map(() => 'sent' as const);
    };
    rec.sendFails.set('1', new TypeError('Network request failed'));
    rec.sendFails.set('2', new TypeError('Network request failed'));
    runner.dispatch([act('setState', '1')], {
      undo: UNDO.none,
      origin: 'tap',
      allOrNothing: false,
    });
    runner.dispatch([act('setState', '2')], {
      undo: UNDO.none,
      origin: 'tap',
      allOrNothing: false,
    });
    await flush();
    expect(byItem(runner, '1')?.state).toBe('uncertain');
    expect(reconciled).toEqual([]);

    goOnline();
    await flush();
    expect(byItem(runner, '1')?.state).toBe('sent');
    expect(byItem(runner, '2')?.state).toBe('sent');
    expect(reconciled).toHaveLength(1);

    // retry(id) of an uncertain action reconciles just that one.
    const rec3 = recorder();
    const third = build(rec3);
    rec3.sendFails.set('7', new TypeError('Network request failed'));
    third.runner.dispatch([act('setState', '7')], {
      undo: UNDO.none,
      origin: 'tap',
      allOrNothing: true,
    });
    await flush();
    const seen: string[][] = [];
    rec3.executor.reconcile = async (items) => {
      seen.push(items.map((p) => p.action.itemId as string));
      return items.map(() => 'sent' as const);
    };
    third.runner.retry((byItem(third.runner, '7') as Pending).id);
    await flush();
    expect(seen).toEqual([['7']]);
    expect(byItem(third.runner, '7')?.state).toBe('sent');
  });

  it('keeps a batch restored before its area registers, unsent and unlisted, and lists it once the area registers', async () => {
    const store = memoryStore();
    // Before a restart: a held tap and a queued action, neither sent (the first runner never started).
    const first = build(recorder(), { store, start: false });
    first.runner.dispatch([act('setState', '1')], {
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
    first.runner.dispatch([act('assign', '2')], {
      undo: UNDO.none,
      origin: 'tap',
      allOrNothing: true,
    });
    const restored = JSON.parse(store.getItem(outboxKey(SCOPE)) as string).items;
    expect(restored).toHaveLength(2);

    // After it: the new runner starts before the Work item area registers its executor.
    const rec = recorder();
    const { runner } = build(rec, { store, register: false });
    await flush();

    // What a toast or banner reads before the area registers: nothing it can't describe, and no throw.
    expect(() => runner.pending().map((p) => runner.describe(p.action))).not.toThrow();
    expect(runner.pending()).toEqual([]);
    expect(rec.calls).toEqual([]);
    expect(JSON.parse(store.getItem(outboxKey(SCOPE)) as string).items).toEqual(restored);

    const heard = vi.fn();
    runner.subscribe(heard);
    runner.register(rec.executor);
    expect(heard).toHaveBeenCalled();
    expect(byItem(runner, '1')?.state).toBe('undo');
    expect(runner.pending().map((p) => runner.describe(p.action).text)).toEqual([
      'setState',
      'assign',
    ]);
    await flush();
    expect(rec.calls).toEqual(['send 2']);

    await vi.advanceTimersByTimeAsync(UNDO.tap.ms);
    expect(rec.calls).toEqual(['send 2', 'send 1']);
  });

  it('tells subscribers when an area registers, even with nothing of its to send', () => {
    const store = memoryStore();
    const first = build(recorder(), { store, start: false });
    first.runner.dispatch([act('setState', '1')], {
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
    const rec = recorder();
    const { runner } = build(rec, { store, register: false });
    const heard = vi.fn();
    runner.subscribe(heard);

    runner.register(rec.executor);

    expect(heard).toHaveBeenCalledTimes(1);
    expect(byItem(runner, '1')?.state).toBe('undo');
  });

  it('tells subscribers on start(), so one that subscribed after an area registered reads pending() again', () => {
    const store = memoryStore();
    build(recorder(), { store, start: false }).runner.dispatch([act('setState', '1')], {
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
    const rec = recorder();
    const { runner } = build(rec, { store, start: false, register: false });
    // As the app mounts: the toast reads pending() before the area registers, and subscribes after it did.
    expect(runner.pending()).toEqual([]);
    runner.register(rec.executor);
    const heard = vi.fn();
    runner.subscribe(heard);

    runner.start();

    expect(heard).toHaveBeenCalled();
    expect(byItem(runner, '1')?.state).toBe('undo');
  });

  it('reconciles what a restart left uncertain when its area registers', async () => {
    const rec = recorder();
    const store = memoryStore();
    const first = build(rec, { store });
    rec.executor.send = () => new Promise(() => undefined);
    first.runner.dispatch([act('setState', '1')], {
      undo: UNDO.none,
      origin: 'tap',
      allOrNothing: true,
    });
    await flush();
    expect(byItem(first.runner, '1')?.state).toBe('sending');
    first.runner.stop();

    const rec2 = recorder();
    rec2.executor.reconcile = async (items) => items.map(() => 'sent' as const);
    const second = build(rec2, { store, start: true });

    await flush();
    expect(byItem(second.runner, '1')?.state).toBe('sent');
    expect(rec2.calls).toEqual([]);
  });

  it('starts from an empty outbox and removes the key when the stored value is corrupt', () => {
    for (const bad of ['{not json', '{"items":5,"batches":{}}', '[]', 'null']) {
      const store = memoryStore();
      store.setItem(outboxKey(SCOPE), bad);

      const { runner } = build(recorder(), { store });

      expect(runner.pending()).toEqual([]);
      expect(store.getItem(outboxKey(SCOPE))).toBeNull();
    }
  });

  it('drops an uncertain action the user undid when reconcile says notSent, instead of resending it', async () => {
    const rec = recorder();
    const { runner, goOnline } = build(rec);
    rec.sendFails.set('1', new TypeError('Network request failed'));
    const asked = vi.fn(async (items: Pending[]) => items.map(() => 'notSent' as const));
    rec.executor.reconcile = asked;
    const batchId = runner.dispatch([act('setState', '1')], {
      undo: UNDO.none,
      origin: 'tap',
      allOrNothing: true,
    });
    await flush();
    rec.sendFails.clear();

    const outcomes = await runner.undo(batchId);
    expect(outcomes.map((o) => o.outcome)).toEqual(['failed']);
    goOnline();
    await flush();

    expect(asked).toHaveBeenCalledTimes(1);
    expect(runner.pending()).toEqual([]);
    expect(rec.calls).toEqual(['send 1']);
  });

  describe('discardUnsent', () => {
    // A voice batch of two whose first send got no answer: 1 is uncertain and 2 waits behind it.
    async function uncertainBatch(rec: ReturnType<typeof recorder>, built = build(rec)) {
      rec.sendFails.set('1', new TypeError('Network request failed'));
      const batchId = built.runner.dispatch([act('setState', '1'), act('assign', '2')], {
        undo: UNDO.voice,
        origin: 'voice',
        allOrNothing: true,
      });
      await flush();
      rec.sendFails.clear();
      expect(byItem(built.runner, '1')?.state).toBe('uncertain');
      expect(byItem(built.runner, '2')?.state).toBe('queued');
      return { ...built, batchId };
    }

    it('drops the queued parts behind a part with no answer at once and tells subscribers', async () => {
      const rec = recorder();
      const { runner, batchId } = await uncertainBatch(rec);
      const heard = vi.fn();
      runner.subscribe(heard);

      runner.discardUnsent(batchId);

      expect(heard).toHaveBeenCalled();
      expect(runner.pending().map((p) => [p.action.itemId, p.state])).toEqual([['1', 'uncertain']]);
    });

    it('drops a held batch, so its window ending sends nothing', async () => {
      const rec = recorder();
      const runner = runnerWith(rec);
      const batchId = runner.dispatch([act('setState', '1')], {
        undo: UNDO.tap,
        origin: 'tap',
        allOrNothing: true,
      });

      runner.discardUnsent(batchId);
      await vi.advanceTimersByTimeAsync(UNDO.tap.ms * 2);

      expect(runner.pending()).toEqual([]);
      expect(rec.calls).toEqual([]);
    });

    it('removes a part with no answer that reconcile says was not sent, instead of sending it again', async () => {
      const rec = recorder();
      rec.executor.reconcile = async (items) => items.map(() => 'notSent' as const);
      const { runner, goOnline, batchId } = await uncertainBatch(rec);

      runner.discardUnsent(batchId);
      goOnline();
      await flush();

      expect(runner.pending()).toEqual([]);
      expect(rec.calls).toEqual(['send 1']);
    });

    it('keeps a part with no answer that reconcile says went through as sent', async () => {
      const rec = recorder();
      rec.executor.reconcile = async (items) => items.map(() => 'sent' as const);
      const { runner, goOnline, batchId } = await uncertainBatch(rec);

      runner.discardUnsent(batchId);
      goOnline();
      await flush();

      expect(runner.pending().map((p) => [p.action.itemId, p.state])).toEqual([['1', 'sent']]);
      expect(rec.calls).toEqual(['send 1']);
    });

    it('leaves sent and failed parts as they are, and retry no longer sends a failed one', async () => {
      const rec = recorder();
      const runner = runnerWith(rec);
      rec.sendFails.set('2', refusal(500, 'internal_error'));
      const batchId = runner.dispatch(
        [act('setState', '1'), act('assign', '2'), act('assign', '3')],
        { undo: UNDO.typed, origin: 'typed', allOrNothing: true },
      );
      await flush();
      rec.sendFails.clear();
      const before = runner.pending();
      expect(before.map((p) => p.state)).toEqual(['sent', 'failed', 'failed']);

      runner.discardUnsent(batchId);
      runner.retry(byItem(runner, '2')?.id as string);
      await flush();

      expect(runner.pending()).toEqual(before);
      expect(rec.calls).toEqual(['send 1', 'send 2']);
    });

    it('keeps the discarded mark over a restart: a part reconcile then finds unsent is removed, not sent', async () => {
      const store = memoryStore();
      const rec = recorder();
      const first = await uncertainBatch(rec, build(rec, { store }));
      first.runner.discardUnsent(first.batchId);
      first.runner.stop();

      const rec2 = recorder();
      rec2.executor.reconcile = async (items) => items.map(() => 'notSent' as const);
      const second = build(rec2, { store });
      await flush();

      expect(second.runner.pending()).toEqual([]);
      expect(rec2.calls).toEqual([]);
    });
  });
});
