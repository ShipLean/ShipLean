// N8: every change the app makes goes through one ActionRunner, whether it comes from a tap, typing or voice, so
// Undo, the offline queue and conflicts behave the same everywhere. Each area owns its own action union in
// src/<area>/actions.ts and registers its Executor in its provider slot.
import type { api } from '@shiplean/core/api/client';

export type Area = 'inbox' | 'workItem' | 'backlog' | 'features' | 'retro';

export interface AppAction {
  area: Area;
  kind: string;
  itemId: string | null;
  key: string | null;
}

export type Api = typeof api;

// One line of a preview or an undo toast, worded by the action's executor.
export interface PreviewLine {
  id: string;
  icon: string;
  text: string;
  detail?: string;
  warning?: string;
  removable: boolean;
}

export type PendingState = 'undo' | 'queued' | 'sending' | 'uncertain' | 'failed' | 'sent';

export type Origin = 'tap' | 'typed' | 'voice';

// hold: wait ms, then send. after: send now, and executor.undo reverses it within ms.
export interface UndoWindow {
  mode: 'hold' | 'after';
  ms: number;
}

export type PendingFailure = 'conflict' | 'validation' | 'notFound' | 'network' | 'server';

export type UndoResult = 'undone' | 'irreversible' | 'failed';

export interface Pending {
  id: string;
  batchId: string;
  action: AppAction;
  origin: Origin;
  state: PendingState;
  // ISO 8601 UTC; null when the action has no undo window.
  undoUntil: string | null;
  error: string | null;
  failure: PendingFailure | null;
  retryable: boolean;
  // ISO 8601 UTC.
  createdAt: string;
  // Set when the batch is undone, or when a failed voice batch is reversed.
  undoOutcome: UndoResult | null;
}

export interface UndoOutcome {
  id: string;
  outcome: UndoResult;
}

export interface Executor<A extends AppAction> {
  area: Area;
  send(a: A, api: Api): Promise<void>;
  describe(a: A): PreviewLine;
  // Required: says which kinds Undo can reverse. A reversible kind needs undo.
  reversible(kind: A['kind']): boolean;
  undo?(a: A, api: Api): Promise<void>;
  // Reloads the area's data and says, per Pending, whether an uncertain send went through.
  reconcile?(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]>;
}

export interface DispatchOptions {
  undo: UndoWindow;
  origin: Origin;
  allOrNothing: boolean;
}

export interface ActionRunner {
  register<A extends AppAction>(e: Executor<A>): void;
  // Returns the batch id.
  dispatch(batch: AppAction[], opts: DispatchOptions): string;
  undo(batchId: string): Promise<UndoOutcome[]>;
  retry(id: string): void;
  pending(): Pending[];
  subscribe(cb: () => void): () => void;
  describe(a: AppAction): PreviewLine;
  subscribeConflicts(cb: (batchId: string, lines: PreviewLine[]) => void): () => void;
}
