// What the runner does beyond runnerRules.test.ts (which pins allOrNothing, 409, after + undo, irreversible, the failed
// voice batch's reversal, the offline queue, reconcile, the 10 minutes, scope and the restart window): the hold rule,
// the failed voice batch's wording, the error copy, the receipt, and the window ending.
import { ApiRequestError } from '@shiplean/core/api/client';
import { memoryStore } from '@shiplean/core/platform';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createActionRunner } from './runner';
import { scopeOf } from './scope';
import type { AppAction, Executor, SendReceipt } from './types';
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

function runnerWith(rec: ReturnType<typeof recorder>) {
  const runner = createActionRunner({
    store: memoryStore(),
    scope: SCOPE,
    isOffline: () => false,
    onOnline: () => () => undefined,
    now: () => new Date(),
  });
  runner.register(rec.executor);
  return runner;
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
    expect(shown('4')).toEqual(['uncertain', 'No answer from ShipLean.', 'network', true]);
    expect(shown('5')).toEqual([
      'failed',
      'Changed on the server. Open SHIP-5 to check.',
      'conflict',
      false,
    ]);
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
});
