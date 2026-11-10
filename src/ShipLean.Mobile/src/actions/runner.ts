import type { KeyValueStore } from '@shiplean/core/platform';
import type { ActionRunner } from './types';

export interface ActionRunnerDeps {
  store: KeyValueStore;
  // origin + email from SessionState: the outbox is kept per scope.
  scope: string;
  isOffline(): boolean;
  onOnline(cb: () => void): () => void;
  now(): Date;
}

// The real runner: sends, queues offline, reconciles and undoes (N8's rules, pinned in runnerRules.test.ts).
export function createActionRunner(deps: ActionRunnerDeps): ActionRunner {
  void deps;
  throw new Error('TODO: the real runner');
}
