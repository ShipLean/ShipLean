// Pure helpers for the action runner (no React Native): a Pending builder and a runner that records what it is asked.
import type {
  ActionRunner,
  AppAction,
  DispatchOptions,
  Executor,
  Pending,
  PreviewLine,
  UndoOutcome,
} from '../actions/types';

export function aPending(overrides: Partial<Pending> = {}): Pending {
  return {
    id: 'pending-1',
    batchId: 'batch-1',
    action: { area: 'workItem', kind: 'setState', itemId: 'item-1', key: 'SHIP-1' },
    origin: 'tap',
    state: 'undo',
    undoUntil: null,
    error: null,
    failure: null,
    retryable: false,
    createdAt: '2030-01-01T09:00:00.000Z',
    undoOutcome: null,
    receipt: null,
    ...overrides,
  };
}

export interface FakeRunner extends ActionRunner {
  readonly dispatched: { batch: AppAction[]; opts: DispatchOptions; batchId: string }[];
  readonly undone: string[];
  readonly retried: string[];
  // Every discardUnsent batch id, in order.
  readonly discarded: string[];
  // Every retireFailed call's ids, in order.
  readonly retired: string[][];
  readonly registered: Executor<AppAction>[];
  // Replaces what pending() returns and notifies subscribers.
  setPending(items: Pending[]): void;
  // Reports a conflict to subscribeConflicts, as the real runner does on a 409.
  emitConflict(batchId: string, lines: PreviewLine[]): void;
}

// Records every call and sends nothing. describe uses a registered executor for the action's area, else the kind.
// Unlike the real runner, pending() lists every item whether or not its area registered: it proves nothing about that rule.
export function fakeRunner(): FakeRunner {
  const dispatched: FakeRunner['dispatched'] = [];
  const undone: string[] = [];
  const retried: string[] = [];
  const discarded: string[] = [];
  const retired: string[][] = [];
  const registered: Executor<AppAction>[] = [];
  const listeners = new Set<() => void>();
  const conflictListeners = new Set<(batchId: string, lines: PreviewLine[]) => void>();
  let items: Pending[] = [];
  const notify = () => {
    for (const cb of [...listeners]) cb();
  };

  return {
    dispatched,
    undone,
    retried,
    discarded,
    retired,
    registered,
    register: (e) => {
      registered.push(e as unknown as Executor<AppAction>);
    },
    dispatch: (batch, opts) => {
      const batchId = `batch-${dispatched.length + 1}`;
      dispatched.push({ batch, opts, batchId });
      return batchId;
    },
    undo: async (batchId): Promise<UndoOutcome[]> => {
      undone.push(batchId);
      const removed = items.filter((p) => p.batchId === batchId);
      items = items.filter((p) => p.batchId !== batchId);
      if (removed.length > 0) notify();
      return removed.reverse().map((p) => ({ id: p.id, outcome: 'undone' }));
    },
    retry: (id) => {
      retried.push(id);
    },
    // Drops the batch's queued and held items, as the real runner does.
    discardUnsent: (batchId) => {
      discarded.push(batchId);
      const kept = items.filter(
        (p) => p.batchId !== batchId || (p.state !== 'queued' && p.state !== 'undo'),
      );
      if (kept.length === items.length) return;
      items = kept;
      notify();
    },
    // Drops the given failed items that were never undone, as the real runner does.
    retireFailed: (ids) => {
      retired.push(ids);
      const kept = items.filter(
        (p) => !(ids.includes(p.id) && p.state === 'failed' && p.undoOutcome === null),
      );
      if (kept.length === items.length) return;
      items = kept;
      notify();
    },
    pending: () => items,
    subscribe: (cb) => {
      listeners.add(cb);
      return () => {
        listeners.delete(cb);
      };
    },
    describe: (a) => {
      const executor = registered.find((e) => e.area === a.area);
      if (executor !== undefined) return executor.describe(a);
      return { id: `${a.area}:${a.kind}:${a.itemId}`, icon: 'dot', text: a.kind, removable: true };
    },
    subscribeConflicts: (cb) => {
      conflictListeners.add(cb);
      return () => {
        conflictListeners.delete(cb);
      };
    },
    setPending: (next) => {
      items = next;
      notify();
    },
    emitConflict: (batchId, lines) => {
      for (const cb of [...conflictListeners]) cb(batchId, lines);
    },
  };
}
