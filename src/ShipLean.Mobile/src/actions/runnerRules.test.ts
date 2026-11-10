// N8's rules for the real runner. Red until the real runner replaces createActionRunner's stub.
import { memoryStore, type KeyValueStore } from '@shiplean/core/platform';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { controlledApi } from '../test-support/controlledApi';
import { restoreFetch } from '../test-support/fakeApi';
import { createActionRunner } from './runner';
import type { ActionRunner, AppAction, Executor, Pending } from './types';
import { UNDO, VOICE_ROW_MS } from './undo';

afterEach(restoreFetch);

const T0 = Date.parse('2030-01-01T09:00:00.000Z');
const SCOPE = 'https://shiplean.test tom@example.com';

const act = (kind: string, itemId: string, area: AppAction['area'] = 'workItem'): AppAction => ({
  area,
  kind,
  itemId,
  key: `SHIP-${itemId}`,
});
const sendRoute = (a: AppAction) => `POST /api/test/${a.kind}/${a.itemId}`;
const undoRoute = (a: AppAction) => `POST /api/test/${a.kind}/${a.itemId}/undo`;

// workItem: approve is irreversible; every other kind is reversible and has an undo.
function workItemExecutor(extra: Partial<Executor<AppAction>> = {}): Executor<AppAction> {
  return {
    area: 'workItem',
    send: (a, api) => api.post(`/api/test/${a.kind}/${a.itemId}`),
    describe: (a) => ({
      id: `${a.kind}-${a.itemId}`,
      icon: 'state',
      text: a.kind,
      removable: true,
    }),
    reversible: (kind) => kind !== 'approve',
    undo: (a, api) => api.post(`/api/test/${a.kind}/${a.itemId}/undo`),
    ...extra,
  };
}

// retro: says every kind is reversible but has no undo.
const retroExecutor: Executor<AppAction> = {
  area: 'retro',
  send: (a, api) => api.post(`/api/test/${a.kind}/${a.itemId}`),
  describe: (a) => ({ id: `${a.kind}-${a.itemId}`, icon: 'retro', text: a.kind, removable: true }),
  reversible: () => true,
};

const ok = { status: 204 };
const conflictBody = {
  error: 'Changed since.',
  code: 'conflict',
  current: { id: '2' },
  theirChanges: [],
};

function world({ store = memoryStore(), scope = SCOPE, offline = false, at = T0 } = {}) {
  const online = new Set<() => void>();
  const state = { offline, at };
  const runner = createActionRunner({
    store,
    scope,
    isOffline: () => state.offline,
    onOnline: (cb) => {
      online.add(cb);
      return () => online.delete(cb);
    },
    now: () => new Date(state.at),
  });
  return {
    runner,
    store,
    state,
    goOnline: () => {
      state.offline = false;
      for (const cb of [...online]) cb();
    },
  };
}

// Resolves once the runner's pending() satisfies the check, re-checking on every notification.
function until(runner: ActionRunner, check: (p: Pending[]) => boolean): Promise<void> {
  return new Promise((resolve) => {
    if (check(runner.pending())) return resolve();
    const stop = runner.subscribe(() => {
      if (check(runner.pending())) {
        stop();
        resolve();
      }
    });
  });
}

const byItem = (pending: Pending[], itemId: string) =>
  pending.find((p) => p.action.itemId === itemId);
const stateOf = (runner: ActionRunner, itemId: string) => byItem(runner.pending(), itemId)?.state;
const settle = async () => {
  for (let i = 0; i < 50; i++) await Promise.resolve();
};

describe('createActionRunner', () => {
  it.fails('R1: sends an allOrNothing batch in order and stops at the first failure', async () => {
    const batch = [act('setState', '1'), act('setState', '2'), act('setState', '3')];
    const api = controlledApi(Object.fromEntries(batch.map((a) => [sendRoute(a), ok])));
    api.failNext(sendRoute(batch[1]), { status: 400, body: { error: 'Bad.', code: 'validation' } });
    const { runner } = world();
    runner.register(workItemExecutor());

    runner.dispatch(batch, { undo: UNDO.none, origin: 'tap', allOrNothing: true });
    await until(runner, (p) => byItem(p, '2')?.state === 'failed');
    await settle();

    expect(api.calls.map((c) => `${c.method} ${c.path}`)).toEqual([
      sendRoute(batch[0]),
      sendRoute(batch[1]),
    ]);
    expect(stateOf(runner, '1')).toBe('sent');
    expect(byItem(runner.pending(), '2')).toMatchObject({ failure: 'validation' });
    expect(stateOf(runner, '3')).not.toBe('sent');
  });

  it.fails(
    'R1: fails a 409 with failure conflict, not retryable, and reports it to subscribeConflicts',
    async () => {
      const batch = [act('setState', '1'), act('setState', '2')];
      controlledApi({
        [sendRoute(batch[0])]: ok,
        [sendRoute(batch[1])]: { status: 409, body: conflictBody },
      });
      const { runner } = world();
      runner.register(workItemExecutor());
      const conflicts = vi.fn();
      runner.subscribeConflicts(conflicts);

      const reported = new Promise<void>((resolve) => runner.subscribeConflicts(() => resolve()));

      const batchId = runner.dispatch(batch, {
        undo: UNDO.none,
        origin: 'tap',
        allOrNothing: true,
      });
      await reported;
      await settle();

      expect(byItem(runner.pending(), '2')).toMatchObject({
        failure: 'conflict',
        retryable: false,
      });
      expect(conflicts).toHaveBeenCalledWith(batchId, [
        runner.describe(batch[0]),
        runner.describe(batch[1]),
      ]);
    },
  );

  it.fails(
    'R1: sends an after batch at once and undo calls executor.undo with an outcome per action',
    async () => {
      const batch = [act('setState', '1'), act('assign', '2')];
      const api = controlledApi(
        Object.fromEntries(
          batch.flatMap((a) => [
            [sendRoute(a), ok],
            [undoRoute(a), ok],
          ]),
        ),
      );
      const { runner } = world();
      runner.register(workItemExecutor());

      const batchId = runner.dispatch(batch, {
        undo: UNDO.typed,
        origin: 'typed',
        allOrNothing: true,
      });
      await until(runner, (p) => p.length === 2 && p.every((x) => x.state === 'sent'));
      const ids = batch.map((a) => byItem(runner.pending(), a.itemId as string)?.id);

      const outcomes = await runner.undo(batchId);

      expect(outcomes).toEqual([
        { id: ids[1], outcome: 'undone' },
        { id: ids[0], outcome: 'undone' },
      ]);
      expect(api.calls.map((c) => `${c.method} ${c.path}`)).toEqual([
        sendRoute(batch[0]),
        sendRoute(batch[1]),
        undoRoute(batch[1]),
        undoRoute(batch[0]),
      ]);
    },
  );

  it.fails(
    'R1: resolves irreversible without a call for a kind the executor marks not reversible, or with no undo',
    async () => {
      const batch = [act('approve', '1'), act('addPoint', '2', 'retro')];
      const api = controlledApi(Object.fromEntries(batch.map((a) => [sendRoute(a), ok])));
      const { runner } = world();
      runner.register(workItemExecutor());
      runner.register(retroExecutor);

      const batchId = runner.dispatch(batch, {
        undo: UNDO.voice,
        origin: 'voice',
        allOrNothing: true,
      });
      await until(runner, (p) => p.length === 2 && p.every((x) => x.state === 'sent'));

      const outcomes = await runner.undo(batchId);

      expect(outcomes.map((o) => o.outcome)).toEqual(['irreversible', 'irreversible']);
      expect(api.calls.filter((c) => c.path.endsWith('/undo'))).toEqual([]);
    },
  );

  it.fails(
    'R1: stops a failed voice batch at the first failure and reverses only the sent reversible actions',
    async () => {
      const batch = [
        act('setState', '1'),
        act('approve', '2'),
        act('setState', '3'),
        act('setState', '4'),
        act('setState', '5'),
      ];
      const api = controlledApi(
        Object.fromEntries(
          batch.flatMap((a) => [
            [sendRoute(a), ok],
            [undoRoute(a), ok],
          ]),
        ),
      );
      api.failNext(sendRoute(batch[3]), {
        status: 400,
        body: { error: 'Bad.', code: 'validation' },
      });
      const { runner } = world();
      runner.register(workItemExecutor());

      runner.dispatch(batch, { undo: UNDO.voice, origin: 'voice', allOrNothing: true });
      await until(
        runner,
        (p) => byItem(p, '1')?.undoOutcome === 'undone' && byItem(p, '3')?.undoOutcome === 'undone',
      );
      await settle();

      expect(api.calls.map((c) => `${c.method} ${c.path}`)).toEqual([
        sendRoute(batch[0]),
        sendRoute(batch[1]),
        sendRoute(batch[2]),
        sendRoute(batch[3]),
        undoRoute(batch[2]),
        undoRoute(batch[0]),
      ]);
      expect(byItem(runner.pending(), '1')?.undoOutcome).toBe('undone');
      expect(byItem(runner.pending(), '2')?.undoOutcome).toBe('irreversible');
      expect(byItem(runner.pending(), '3')?.undoOutcome).toBe('undone');
      expect(byItem(runner.pending(), '4')).toMatchObject({
        state: 'failed',
        failure: 'validation',
      });
      expect(stateOf(runner, '5')).not.toBe('sent');
    },
  );

  it.fails(
    'R1: queues offline and sends one by one when back online, carrying on past a refusal',
    async () => {
      const actions = [act('setState', '1'), act('setState', '2'), act('setState', '3')];
      const api = controlledApi(Object.fromEntries(actions.map((a) => [sendRoute(a), ok])));
      api.failNext(sendRoute(actions[1]), {
        status: 400,
        body: { error: 'Bad.', code: 'validation' },
      });
      const w = world({ offline: true });
      w.runner.register(workItemExecutor());

      for (const a of actions)
        w.runner.dispatch([a], { undo: UNDO.none, origin: 'tap', allOrNothing: false });
      await settle();
      expect(api.calls).toEqual([]);
      expect(w.runner.pending().map((p) => p.state)).toEqual(['queued', 'queued', 'queued']);

      w.goOnline();
      await until(w.runner, (p) => byItem(p, '3')?.state === 'sent');

      expect(api.calls.map((c) => `${c.method} ${c.path}`)).toEqual(actions.map(sendRoute));
      expect(stateOf(w.runner, '1')).toBe('sent');
      expect(byItem(w.runner.pending(), '2')).toMatchObject({
        state: 'failed',
        failure: 'validation',
      });
    },
  );

  it.fails(
    'R1: marks a send with no response uncertain and resends it only when reconcile says notSent or retry(id) is called',
    async () => {
      const a = act('setState', '1');

      // reconcile says it went through: settled with no resend.
      let api = controlledApi({ [sendRoute(a)]: ok });
      api.offline(true);
      let w = world();
      const saysSent = vi.fn(async (items: Pending[]) => items.map(() => 'sent' as const));
      w.runner.register(workItemExecutor({ reconcile: saysSent }));
      w.runner.dispatch([a], { undo: UNDO.none, origin: 'tap', allOrNothing: true });
      await until(w.runner, (p) => p[0]?.state === 'sent');
      expect(saysSent).toHaveBeenCalledWith(
        [expect.objectContaining({ state: 'uncertain', action: a })],
        expect.anything(),
      );
      expect(api.callsTo(sendRoute(a))).toHaveLength(0);
      restoreFetch();

      // reconcile says it didn't: resent once.
      api = controlledApi({ [sendRoute(a)]: ok });
      api.offline(true);
      w = world();
      const saysNotSent = vi.fn(async (items: Pending[]) => {
        api.offline(false);
        return items.map(() => 'notSent' as const);
      });
      w.runner.register(workItemExecutor({ reconcile: saysNotSent }));
      w.runner.dispatch([a], { undo: UNDO.none, origin: 'tap', allOrNothing: true });
      await until(w.runner, (p) => p[0]?.state === 'sent');
      expect(api.callsTo(sendRoute(a))).toHaveLength(1);
      restoreFetch();

      // No reconcile: it waits as uncertain until retry(id).
      api = controlledApi({ [sendRoute(a)]: ok });
      api.offline(true);
      w = world();
      w.runner.register(workItemExecutor());
      w.runner.dispatch([a], { undo: UNDO.none, origin: 'tap', allOrNothing: true });
      await until(w.runner, (p) => p[0]?.state === 'uncertain');
      api.offline(false);
      await settle();
      expect(w.runner.pending()[0].state).toBe('uncertain');
      expect(api.callsTo(sendRoute(a))).toHaveLength(0);

      w.runner.retry(w.runner.pending()[0].id);
      await until(w.runner, (p) => p[0]?.state === 'sent');
      expect(api.callsTo(sendRoute(a))).toHaveLength(1);
    },
  );

  it.fails('R1: keeps sent entries for 10 minutes', async () => {
    const a = act('setState', '1');
    controlledApi({ [sendRoute(a)]: ok });
    const w = world();
    w.runner.register(workItemExecutor());

    w.runner.dispatch([a], { undo: UNDO.none, origin: 'voice', allOrNothing: true });
    await until(w.runner, (p) => p[0]?.state === 'sent');

    w.state.at = T0 + VOICE_ROW_MS - 1;
    expect(w.runner.pending()).toHaveLength(1);
    w.state.at = T0 + VOICE_ROW_MS + 1;
    expect(w.runner.pending()).toEqual([]);
  });

  it.fails('R1: keeps the outbox per scope (origin + email)', async () => {
    controlledApi({});
    const store: KeyValueStore = memoryStore();
    const tom = world({ store, offline: true });
    tom.runner.register(workItemExecutor());
    tom.runner.dispatch([act('setState', '1')], {
      undo: UNDO.none,
      origin: 'tap',
      allOrNothing: true,
    });

    const amy = world({ store, offline: true, scope: 'https://shiplean.test amy@example.com' });
    expect(amy.runner.pending()).toEqual([]);

    const tomAgain = world({ store, offline: true });
    expect(tomAgain.runner.pending().map((p) => [p.action, p.state])).toEqual([
      [act('setState', '1'), 'queued'],
    ]);
  });

  it.fails('R1: gives undo-window items a fresh window after a restart', () => {
    controlledApi({});
    const store: KeyValueStore = memoryStore();
    const before = world({ store, offline: true });
    before.runner.register(workItemExecutor());
    before.runner.dispatch([act('setState', '1')], {
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });

    const restartAt = T0 + 60_000;
    const after = world({ store, offline: true, at: restartAt });

    expect(after.runner.pending().map((p) => [p.state, p.undoUntil])).toEqual([
      ['undo', new Date(restartAt + UNDO.tap.ms).toISOString()],
    ]);
  });
});
