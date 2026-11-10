import type {
  ActionRunner,
  AppAction,
  Area,
  DispatchOptions,
  Executor,
  Pending,
  PreviewLine,
  UndoOutcome,
} from './types';

export interface InMemoryActionRunnerDeps {
  now: () => Date;
}

// Records dispatches and holds each batch in `undo` until its window ends, then marks it queued. It never sends.
// TODO: the real runner (createActionRunner) sends, queues offline, reconciles and undoes sent actions.
// TODO: pending() works out the end of an undo window lazily and doesn't notify subscribers when it ends; the real runner does.
export class InMemoryActionRunner implements ActionRunner {
  private readonly now: () => Date;
  private readonly executors = new Map<Area, Executor<AppAction>>();
  private readonly listeners = new Set<() => void>();
  private readonly conflictListeners = new Set<(batchId: string, lines: PreviewLine[]) => void>();
  private items: Pending[] = [];
  private readonly discarded = new Set<string>();
  private nextBatch = 1;
  private nextItem = 1;

  constructor({ now }: InMemoryActionRunnerDeps) {
    this.now = now;
  }

  register<A extends AppAction>(e: Executor<A>): void {
    const existing = this.executors.get(e.area);
    if (existing === (e as unknown as Executor<AppAction>)) return;
    if (existing !== undefined)
      throw new Error(`Another executor is already registered for ${e.area}.`);
    this.executors.set(e.area, e as unknown as Executor<AppAction>);
    this.notify();
  }

  dispatch(batch: AppAction[], opts: DispatchOptions): string {
    const batchId = `batch-${this.nextBatch++}`;
    const at = this.now();
    const undoUntil = opts.undo.ms > 0 ? new Date(at.getTime() + opts.undo.ms).toISOString() : null;
    for (const action of batch) {
      this.items.push({
        id: `${batchId}-${this.nextItem++}`,
        batchId,
        action,
        origin: opts.origin,
        state: undoUntil === null ? 'queued' : 'undo',
        undoUntil,
        error: null,
        failure: null,
        retryable: false,
        createdAt: at.toISOString(),
        undoOutcome: null,
        receipt: null,
      });
    }
    this.notify();
    return batchId;
  }

  // Nothing was ever sent, so every action of the batch is undone by dropping it.
  async undo(batchId: string): Promise<UndoOutcome[]> {
    const removed = this.items.filter((p) => p.batchId === batchId);
    if (removed.length === 0) return [];
    this.items = this.items.filter((p) => p.batchId !== batchId);
    this.notify();
    return removed.reverse().map((p) => ({ id: p.id, outcome: 'undone' }));
  }

  retry(id: string): void {
    const item = this.items.find((p) => p.id === id);
    if (item === undefined || (item.state !== 'failed' && item.state !== 'uncertain')) return;
    // As the real runner: a discarded batch's part with no answer is never queued again; a failed one's Retry is.
    if (item.state === 'uncertain' && this.discarded.has(item.batchId)) return;
    this.items = this.items.map((p) =>
      p.id === id ? { ...p, state: 'queued', error: null, failure: null } : p,
    );
    this.notify();
  }

  // As the real runner: the batch's queued and held actions go, and only an explicit retry of a failed one queues it. This runner never
  // sends, so there is nothing uncertain to settle.
  discardUnsent(batchId: string): void {
    if (!this.items.some((p) => p.batchId === batchId)) return;
    this.discarded.add(batchId);
    this.items = this.items.filter(
      (p) => p.batchId !== batchId || (p.state !== 'queued' && p.state !== 'undo'),
    );
    this.notify();
  }

  pending(): Pending[] {
    const now = this.now().getTime();
    // As the real runner: only the actions of areas whose executor has registered.
    return this.items
      .filter((p) => this.executors.has(p.action.area))
      .map((p) =>
        p.state === 'undo' && p.undoUntil !== null && Date.parse(p.undoUntil) <= now
          ? { ...p, state: 'queued' }
          : p,
      );
  }

  subscribe(cb: () => void): () => void {
    this.listeners.add(cb);
    return () => {
      this.listeners.delete(cb);
    };
  }

  describe(a: AppAction): PreviewLine {
    const executor = this.executors.get(a.area);
    if (executor === undefined) throw new Error(`No executor is registered for ${a.area}.`);
    return executor.describe(a);
  }

  // Only a sent action can conflict, and this runner never sends.
  subscribeConflicts(cb: (batchId: string, lines: PreviewLine[]) => void): () => void {
    this.conflictListeners.add(cb);
    return () => {
      this.conflictListeners.delete(cb);
    };
  }

  private notify(): void {
    for (const cb of [...this.listeners]) cb();
  }
}
