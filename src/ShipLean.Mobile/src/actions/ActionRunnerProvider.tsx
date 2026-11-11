import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import type { ActionRunner, Pending } from './types';

const RunnerContext = createContext<ActionRunner | null>(null);

// The per-session runner (built in src/shell/providers/runner.tsx). There is no module-level runner.
export function ActionRunnerProvider({
  runner,
  children,
}: {
  runner: ActionRunner;
  children: ReactNode;
}) {
  return <RunnerContext.Provider value={runner}>{children}</RunnerContext.Provider>;
}

export function useActionRunner(): ActionRunner {
  const runner = useContext(RunnerContext);
  if (runner === null) throw new Error('useActionRunner needs an ActionRunnerProvider above it.');
  return runner;
}

// Renders again whenever the runner notifies its subscribers.
export function useRunnerChanges(runner: ActionRunner): void {
  const [, setVersion] = useState(0);
  useEffect(() => runner.subscribe(() => setVersion((v) => v + 1)), [runner]);
}

// The runner's pending actions, optionally filtered, kept current.
export function usePending(filter?: (p: Pending) => boolean): Pending[] {
  const runner = useActionRunner();
  useRunnerChanges(runner);
  const all = runner.pending();
  return filter === undefined ? all : all.filter(filter);
}
