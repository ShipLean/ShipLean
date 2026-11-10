import { ApiRequestError, api } from '@shiplean/core/api/client';
import type { KeyValueStore } from '@shiplean/core/platform';
import { isNoAnswer, sendError } from './errors';
import {
  addBatch,
  discard,
  emptyOutbox,
  isDiscarded,
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

// The runner of one session. It is inert until start(): it has only read its outbox, so a runner built in a render
// React throws away does nothing. stop() is for its owner (runner.tsx) when the session ends or the slot unmounts: no
// timer, connection or late answer touches the outbox after it. start() runs it again (an effect that runs again over
// the same runner).
export interface SessionRunner extends ActionRunner {
  start(): void;
  stop(): void;
}

// The real runner: sends, queues offline, reconciles and undoes (N8's rules, pinned in runnerRules.test.ts).
export function createActionRunner(deps: ActionRunnerDeps): SessionRunner {
  const { store, now } = deps;
  const key = outboxKey(deps.scope);
  let box: Outbox = emptyOutbox;
  try {
    box = load(store.getItem(key), now());
  } catch {
    // A corrupt value would throw on every launch: it goes, and the runner starts empty.
    store.removeItem(key);
  }
  const executors = new Map<Area, Executor<AppAction>>();
  const listeners = new Set<() => void>();
  const conflictListeners = new Set<(batchId: string, lines: PreviewLine[]) => void>();
  const timers = new Map<string, ReturnType<typeof setTimeout>>();
  // Inert before start() and after stop(): nothing is sent, no timer or connection listener runs.
  let stopped = true;
  // Set by stop() only: nothing writes the outbox after that. Before start(), dispatch still keeps what it is given.
  let halted = false;
  let pumping = false;
  // Ids stay distinct from the ones a restart loaded.
  const prefix = `${now().getTime().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
  let seq = 0;
  const nextId = () => `${prefix}-${++seq}`;

  function notify() {
    for (const cb of [...listeners]) cb();
  }

  function set(next: Outbox) {
    if (halted) return;
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
              (q.state === 'sending' ||
                q.state === 'uncertain' ||
                q.state === 'undo' ||
                q.state === 'queued'),
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
    if (!(e instanceof ApiRequestError) && isNoAnswer(e)) {
      // Offline: it never got out, so it waits for the connection. Online: it may have gone through, so it is
      // uncertain with no failure yet; onOnline, its area registering or retry(id) settles it.
      // A discarded batch sends nothing more: a part that never got out goes.
      if (deps.isOffline())
        set(
          isDiscarded(box, p.batchId)
            ? remove(box, [p.id])
            : update(box, p.id, { state: 'queued', ...mapped }),
        );
      else set(update(box, p.id, { state: 'uncertain', ...mapped, failure: null }));
      return;
    }
    const batch = box.batches[p.batchId];
    const stopsBatch = batch !== undefined && batch.live && batch.allOrNothing;
    if (stopsBatch && p.origin === 'voice') {
      // A live voice batch is reversed before anyone hears of the refusal: the refused part stays 'sending' (which
      // also holds the parts queued behind it) until every sent part has its undo outcome. Then the refused part and
      // its followers fail in one set(), so a subscriber sees the whole batch settled, and only then are conflicts told.
      // The refusal is kept first, still 'sending' (nothing keys on the failure of a part that is sending): if the app
      // dies while it reverses, load() finds it and fails the part and its followers instead of sending them again.
      set(update(box, p.id, { ...mapped }));
      // An executor or the store that throws while it reverses: not every undo ran, so something may have stayed.
      let stayed: boolean;
      try {
        stayed = await reverseVoiceBatch(p);
      } catch {
        stayed = true;
      }
      let next = update(box, p.id, {
        state: 'failed',
        ...mapped,
        error: `${stayed ? 'Partly applied.' : 'Nothing changed.'} ${mapped.error}`,
      });
      for (const q of ofBatch(p.batchId))
        if (q.state === 'queued')
          next = update(next, q.id, {
            state: 'failed',
            error: null,
            failure: null,
            retryable: false,
          });
      set(next);
      tellConflict(p, mapped.failure);
      return;
    }
    set(update(box, p.id, { state: 'failed', ...mapped }));
    tellConflict(p, mapped.failure);
    if (!stopsBatch) return;
    for (const q of ofBatch(p.batchId))
      if (q.state === 'queued')
        set(update(box, q.id, { state: 'failed', error: null, failure: null, retryable: false }));
  }

  function tellConflict(p: Pending, failure: Pending['failure']) {
    if (failure !== 'conflict') return;
    const lines = ofBatch(p.batchId).map((q) => describe(q.action));
    for (const cb of [...conflictListeners]) cb(p.batchId, lines);
  }

  // A refused live voice batch: reverse what was sent and has a reverse, newest first. Says whether anything stayed.
  // Throws when the executor's reversible() or the store does (undoSent catches what undo itself throws).
  async function reverseVoiceBatch(failed: Pending): Promise<boolean> {
    const sent = ofBatch(failed.batchId)
      .filter((q) => q.state === 'sent')
      .reverse();
    let stayed = false;
    for (const q of sent) {
      const outcome = await undoSent(q);
      if (outcome !== 'undone') stayed = true;
      set(update(box, q.id, { undoOutcome: outcome }));
    }
    return stayed;
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
    if (stopped || executor?.reconcile === undefined || deps.isOffline()) return;
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
      // Only an explicit verdict settles it; a missing one keeps it uncertain.
      if (verdicts[i] === 'sent') set(markSent(box, p.id, null, now()));
      // The user undid it while it was uncertain, or discarded its batch: it is dropped, not resent.
      else if (verdicts[i] === 'notSent')
        set(
          p.undoOutcome === null && !isDiscarded(box, p.batchId)
            ? requeue(box, p.id)
            : remove(box, [p.id]),
        );
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

  let stopOnline = () => {};

  // Listens for the connection, watches the open undo windows and sends what is ready. Runs from start() only.
  function resume() {
    stopOnline = deps.onOnline(() => {
      if (stopped) return;
      set(notLive(box));
      reconcileAll();
      void pump();
    });
    for (const p of box.items) {
      if (p.undoUntil === null || timers.has(p.batchId)) continue;
      if (p.state === 'undo') watchWindow(p.batchId, p.undoUntil, 'hold');
      else if (Date.parse(p.undoUntil) > now().getTime())
        watchWindow(p.batchId, p.undoUntil, 'after');
    }
    reconcileAll();
    void pump();
  }

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
      // What the outbox kept for this area shows in pending() from now on.
      notify();
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
        // Undone and irreversible are final; a sent action whose undo failed (offline, a 5xx) is tried again.
        if (
          current.undoOutcome !== null &&
          !(current.undoOutcome === 'failed' && current.state === 'sent')
        )
          outcome = current.undoOutcome;
        else if (current.state === 'queued' || current.state === 'failed') {
          set(remove(box, [p.id]));
          outcome = 'undone';
        } else if (current.state === 'sent') {
          outcome = await undoSent(current);
          set(update(box, p.id, { undoOutcome: outcome, undoUntil: null }));
        } else if (current.state === 'uncertain' || current.state === 'sending') {
          // It may have gone through: it can't be reversed, and reconcile must not resend it.
          outcome = 'failed';
          set(update(box, p.id, { undoOutcome: outcome }));
        } else outcome = 'failed';
        outcomes.push({ id: p.id, outcome });
      }
      return outcomes;
    },

    retry(id: string): void {
      const p = find(id);
      // Undone by the user: never sent again.
      if (p === undefined || p.undoOutcome !== null) return;
      if (p.state === 'uncertain' && executors.get(p.action.area)?.reconcile !== undefined) {
        void reconcile(p.action.area, [id]);
        return;
      }
      if (p.state !== 'uncertain' && p.state !== 'failed') return;
      // Discarded: a part with no answer is never queued again. A failed part is: its Retry is a new, explicit choice.
      if (p.state === 'uncertain' && isDiscarded(box, p.batchId)) return;
      set(requeue(box, id));
      void pump();
    },

    discardUnsent(batchId: string): void {
      if (ofBatch(batchId).length === 0) return;
      // A hold that is dropped has no window left to end.
      if (ofBatch(batchId).some((p) => p.state === 'undo')) {
        clearTimeout(timers.get(batchId));
        timers.delete(batchId);
      }
      set(discard(box, batchId));
    },

    retireFailed(ids: string[]): void {
      const gone = ids.filter((id) => {
        const p = find(id);
        return p?.state === 'failed' && p.undoOutcome === null;
      });
      if (gone.length > 0) set(remove(box, gone));
    },

    // An area's actions wait, unsent and unlisted, until its executor registers: a batch the outbox kept from before a
    // restart can be loaded before the area provider's effect registers it, and nothing could describe it yet.
    pending: () => visible(box, now()).filter((p) => executors.has(p.action.area)),

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

    start(): void {
      if (!stopped) return;
      stopped = false;
      halted = false;
      // The owner starts the runner in its effect, which runs after every effect below it: the area providers have
      // registered by now, and a subscriber that rendered before they did (the undo toast) has subscribed since and
      // missed register's notification. It reads pending() again.
      notify();
      resume();
    },

    stop(): void {
      stopped = true;
      halted = true;
      for (const t of timers.values()) clearTimeout(t);
      timers.clear();
      stopOnline();
      stopOnline = () => {};
    },
  };
}
