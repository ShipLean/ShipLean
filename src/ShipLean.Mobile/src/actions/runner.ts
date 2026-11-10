import { ApiRequestError, api } from '@shiplean/core/api/client';
import type { KeyValueStore } from '@shiplean/core/platform';
import { sendError } from './errors';
import {
  addBatch,
  load,
  markSent,
  notLive,
  outboxKey,
  prune,
  releaseHeld,
  remove,
  requeue,
  serialise,
  update,
  visible,
  type Outbox,
} from './outbox';
import type {
  ActionRunner,
  AppAction,
  Area,
  Executor,
  Pending,
  PreviewLine,
  UndoOutcome,
  UndoResult,
} from './types';

export interface ActionRunnerDeps {
  store: KeyValueStore;
  // origin + email from SessionState: the outbox is kept per scope.
  scope: string;
  isOffline(): boolean;
  onOnline(cb: () => void): () => void;
  now(): Date;
}

// The runner of one session. stop() is for its owner (runner.tsx) when the session ends or the slot unmounts: no
// timer, connection or late answer touches the outbox after it.
export interface SessionRunner extends ActionRunner {
  stop(): void;
}

// The real runner: sends, queues offline, reconciles and undoes (N8's rules, pinned in runnerRules.test.ts).
export function createActionRunner(deps: ActionRunnerDeps): SessionRunner {
  const { store, now } = deps;
  const key = outboxKey(deps.scope);
  let box: Outbox = load(store.getItem(key), now());
  const executors = new Map<Area, Executor<AppAction>>();
  const listeners = new Set<() => void>();
  const conflictListeners = new Set<(batchId: string, lines: PreviewLine[]) => void>();
  const timers = new Map<string, ReturnType<typeof setTimeout>>();
  let stopped = false;
  let pumping = false;
  // Ids stay distinct from the ones a restart loaded.
  const prefix = `${now().getTime().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
  let seq = 0;
  const nextId = () => `${prefix}-${++seq}`;

  function notify() {
    for (const cb of [...listeners]) cb();
  }

  function set(next: Outbox) {
    if (stopped) return;
    box = prune(next, now());
    const raw = serialise(box);
    if (raw === null) store.removeItem(key);
    else store.setItem(key, raw);
    notify();
  }

  const find = (id: string) => box.items.find((p) => p.id === id);
  const ofBatch = (batchId: string) => box.items.filter((p) => p.batchId === batchId);

  // A hold sends when its window ends; an after window only closes, and subscribers hear it.
  function watchWindow(batchId: string, undoUntil: string, mode: 'hold' | 'after') {
    clearTimeout(timers.get(batchId));
    const delay = Math.max(0, Date.parse(undoUntil) - now().getTime());
    timers.set(
      batchId,
      setTimeout(() => {
        timers.delete(batchId);
        if (stopped) return;
        if (mode === 'hold') {
          set(releaseHeld(box, batchId));
          void pump();
        } else notify();
      }, delay),
    );
  }

  for (const p of box.items) {
    if (p.undoUntil === null || timers.has(p.batchId)) continue;
    if (p.state === 'undo') watchWindow(p.batchId, p.undoUntil, 'hold');
    else if (Date.parse(p.undoUntil) > now().getTime())
      watchWindow(p.batchId, p.undoUntil, 'after');
  }

  // The first queued action, in dispatch order, whose area can send it and that no earlier action of its batch is
  // still waiting on.
  function nextToSend(): Pending | undefined {
    return box.items.find(
      (p, i) =>
        p.state === 'queued' &&
        executors.has(p.action.area) &&
        !box.items
          .slice(0, i)
          .some(
            (q) =>
              q.batchId === p.batchId &&
              (q.state === 'sending' || q.state === 'uncertain' || q.state === 'undo'),
          ),
    );
  }

  async function pump(): Promise<void> {
    if (pumping) return;
    pumping = true;
    try {
      for (;;) {
        if (stopped || deps.isOffline()) return;
        const next = nextToSend();
        if (next === undefined) return;
        await sendOne(next);
      }
    } finally {
      pumping = false;
    }
  }

  async function sendOne(p: Pending): Promise<void> {
    const executor = executors.get(p.action.area) as Executor<AppAction>;
    set(update(box, p.id, { state: 'sending' }));
    let receipt;
    try {
      receipt = await executor.send(p.action, api);
    } catch (e) {
      await refused(p, e);
      return;
    }
    set(markSent(box, p.id, receipt ?? null, now()));
  }

  async function refused(p: Pending, e: unknown): Promise<void> {
    const mapped = sendError(e, p.action);
    if (!(e instanceof ApiRequestError)) {
      // Offline: it never got out, so it waits for the connection. Online: it may have gone through.
      if (deps.isOffline()) set(update(box, p.id, { state: 'queued', ...mapped }));
      else {
        set(update(box, p.id, { state: 'uncertain', ...mapped }));
        void reconcile(p.action.area, [p.id]);
      }
      return;
    }
    set(update(box, p.id, { state: 'failed', ...mapped }));
    if (mapped.failure === 'conflict') {
      const lines = ofBatch(p.batchId).map((q) => describe(q.action));
      for (const cb of [...conflictListeners]) cb(p.batchId, lines);
    }
    const batch = box.batches[p.batchId];
    if (batch === undefined || !batch.live || !batch.allOrNothing) return;
    for (const q of ofBatch(p.batchId))
      if (q.state === 'queued')
        set(update(box, q.id, { state: 'failed', error: null, failure: null, retryable: false }));
    if (p.origin === 'voice') await reverseVoiceBatch(p, mapped.error);
  }

  // A failed online voice batch: reverse what was sent and has a reverse, newest first, and say whether anything stayed.
  async function reverseVoiceBatch(failed: Pending, reason: string): Promise<void> {
    const sent = ofBatch(failed.batchId)
      .filter((q) => q.state === 'sent')
      .reverse();
    let stayed = false;
    for (const q of sent) {
      const outcome = await undoSent(q);
      if (outcome !== 'undone') stayed = true;
      set(update(box, q.id, { undoOutcome: outcome }));
    }
    set(
      update(box, failed.id, {
        error: `${stayed ? 'Partly applied.' : 'Nothing changed.'} ${reason}`,
      }),
    );
  }

  async function undoSent(p: Pending): Promise<UndoResult> {
    const executor = executors.get(p.action.area);
    if (executor?.undo === undefined || !executor.reversible(p.action.kind)) return 'irreversible';
    try {
      await executor.undo(p.action, api, p.receipt);
      return 'undone';
    } catch {
      return 'failed';
    }
  }

  // Asks the area whether its uncertain sends went through: sent settles, notSent goes back in the queue once.
  async function reconcile(area: Area, ids: string[]): Promise<void> {
    const executor = executors.get(area);
    if (executor?.reconcile === undefined || deps.isOffline()) return;
    const items = ids.map(find).filter((p): p is Pending => p?.state === 'uncertain');
    if (items.length === 0) return;
    let verdicts: ('sent' | 'notSent')[];
    try {
      verdicts = await executor.reconcile(items, api);
    } catch {
      // Still no answer: they stay uncertain until the connection comes back or retry(id).
      return;
    }
    items.forEach((p, i) => {
      if (find(p.id)?.state !== 'uncertain') return;
      set(verdicts[i] === 'sent' ? markSent(box, p.id, null, now()) : requeue(box, p.id));
    });
    void pump();
  }

  function reconcileAll(area?: Area) {
    const uncertain = box.items.filter(
      (p) => p.state === 'uncertain' && (area === undefined || p.action.area === area),
    );
    const areas = new Set(uncertain.map((p) => p.action.area));
    for (const a of areas)
      void reconcile(
        a,
        uncertain.filter((p) => p.action.area === a).map((p) => p.id),
      );
  }

  const stopOnline = deps.onOnline(() => {
    if (stopped) return;
    set(notLive(box));
    reconcileAll();
    void pump();
  });

  function describe(a: AppAction): PreviewLine {
    const executor = executors.get(a.area);
    if (executor === undefined) throw new Error(`No executor is registered for ${a.area}.`);
    return executor.describe(a);
  }

  return {
    register<A extends AppAction>(e: Executor<A>): void {
      const existing = executors.get(e.area);
      if (existing === (e as unknown as Executor<AppAction>)) return;
      if (existing !== undefined)
        throw new Error(`Another executor is already registered for ${e.area}.`);
      executors.set(e.area, e as unknown as Executor<AppAction>);
      reconcileAll(e.area);
      void pump();
    },

    dispatch(batch, opts): string {
      const batchId = nextId();
      const actions = batch.map((action) => ({ id: nextId(), action }));
      set(addBatch(box, batchId, actions, opts, now(), deps.isOffline()));
      const undoUntil = ofBatch(batchId)[0]?.undoUntil ?? null;
      if (undoUntil !== null) watchWindow(batchId, undoUntil, opts.undo.mode);
      void pump();
      return batchId;
    },

    async undo(batchId: string): Promise<UndoOutcome[]> {
      const items = ofBatch(batchId).reverse();
      clearTimeout(timers.get(batchId));
      timers.delete(batchId);
      // Still held: nothing went out, so dropping it undoes it.
      if (items.some((p) => p.state === 'undo')) {
        set(
          remove(
            box,
            items.map((p) => p.id),
          ),
        );
        return items.map((p) => ({ id: p.id, outcome: 'undone' }));
      }
      const outcomes: UndoOutcome[] = [];
      for (const p of items) {
        const current = find(p.id) ?? p;
        let outcome: UndoResult;
        if (current.undoOutcome !== null) outcome = current.undoOutcome;
        else if (current.state === 'queued' || current.state === 'failed') {
          set(remove(box, [p.id]));
          outcome = 'undone';
        } else if (current.state === 'sent') {
          outcome = await undoSent(current);
          set(update(box, p.id, { undoOutcome: outcome, undoUntil: null }));
        } else outcome = 'failed';
        outcomes.push({ id: p.id, outcome });
      }
      return outcomes;
    },

    retry(id: string): void {
      const p = find(id);
      if (p === undefined) return;
      if (p.state === 'uncertain' && executors.get(p.action.area)?.reconcile !== undefined) {
        void reconcile(p.action.area, [id]);
        return;
      }
      if (p.state !== 'uncertain' && p.state !== 'failed') return;
      set(requeue(box, id));
      void pump();
    },

    pending: () => visible(box, now()),

    subscribe(cb: () => void): () => void {
      listeners.add(cb);
      return () => {
        listeners.delete(cb);
      };
    },

    describe,

    subscribeConflicts(cb: (batchId: string, lines: PreviewLine[]) => void): () => void {
      conflictListeners.add(cb);
      return () => {
        conflictListeners.delete(cb);
      };
    },

    stop(): void {
      stopped = true;
      for (const t of timers.values()) clearTimeout(t);
      timers.clear();
      stopOnline();
    },
  };
}
