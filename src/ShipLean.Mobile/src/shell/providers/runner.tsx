import { platform } from '@shiplean/core/platform';
import { useEffect, useState } from 'react';
import { systemNow } from '../../actions/clock';
import { ActionRunnerProvider } from '../../actions/ActionRunnerProvider';
import { createActionRunner, type SessionRunner } from '../../actions/runner';
import { scopeOf } from '../../actions/scope';
import { UndoToastHost } from '../../actions/UndoToastHost';
import { session } from '../../session/session';
import { useSession } from '../../session/useSession';
import { ToastHost } from '../../ui/Toast';
import type { ProjectSlotProps } from './types';

// One runner per signed-in session, over that session's outbox in the kv store: a new one is built when the origin or
// the email changes, and on a remount (a restart builds a new runner over the same kv values). The old one stops, so
// two runners never send the same outbox. It also stops the moment the session is lost, before lostSession clears
// the outbox, so a late answer can't write it back.
// No project route renders while signed out, so without an origin and an email the children get no runner.
export function RunnerProvider({ children }: ProjectSlotProps) {
  const { origin, email } = useSession();
  const scope = scopeOf({ origin, email });
  const [runner, setRunner] = useState<{ scope: string; runner: SessionRunner } | null>(null);

  useEffect(() => {
    if (scope === null) return;
    const built = createActionRunner({
      store: platform().persistentStore(),
      scope,
      isOffline: () => platform().isOffline(),
      onOnline: (cb) => platform().onOnline(cb),
      now: systemNow,
    });
    setRunner({ scope, runner: built });
    const stopOnSignOut = session.subscribe(() => {
      if (!session.get().signedIn) built.stop();
    });
    return () => {
      stopOnSignOut();
      built.stop();
    };
  }, [scope]);

  if (scope === null) return children;
  // The first render, and the one after the scope changes, wait for the runner of this scope.
  if (runner === null || runner.scope !== scope) return null;
  return (
    <ActionRunnerProvider runner={runner.runner}>
      <ToastHost>
        {children}
        <UndoToastHost />
      </ToastHost>
    </ActionRunnerProvider>
  );
}
