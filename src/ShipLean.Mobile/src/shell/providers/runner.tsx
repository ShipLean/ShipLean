import { platform } from '@shiplean/core/platform';
import { useEffect, useMemo } from 'react';
import { systemNow } from '../../actions/clock';
import { ActionRunnerProvider } from '../../actions/ActionRunnerProvider';
import { createActionRunner } from '../../actions/runner';
import { scopeOf } from '../../actions/scope';
import { UndoToastHost } from '../../actions/UndoToastHost';
import { session } from '../../session/session';
import { useSession } from '../../session/useSession';
import { ToastHost } from '../../ui/Toast';
import type { ProjectSlotProps } from './types';

// One runner per signed-in session, over that session's outbox in the kv store: a new one is built when the origin or
// the email changes, and on a remount (a restart builds a new runner over the same kv values). The effect stops it
// when the slot unmounts or the scope changes, so two runners never send the same outbox, and the moment the session
// is lost, before lostSession clears the outbox, so a late answer can't write it back.
// No project route renders while signed out, so without an origin and an email the children get no runner.
export function RunnerProvider({ children }: ProjectSlotProps) {
  const { origin, email } = useSession();
  const scope = scopeOf({ origin, email });
  const runner = useMemo(
    () =>
      scope === null
        ? null
        : createActionRunner({
            store: platform().persistentStore(),
            scope,
            isOffline: () => platform().isOffline(),
            onOnline: (cb) => platform().onOnline(cb),
            now: systemNow,
          }),
    [scope],
  );

  useEffect(() => {
    if (runner === null) return;
    runner.start();
    const stopOnSignOut = session.subscribe(() => {
      if (!session.get().signedIn) runner.stop();
    });
    return () => {
      stopOnSignOut();
      runner.stop();
    };
  }, [runner]);

  if (runner === null) return children;
  return (
    <ActionRunnerProvider runner={runner}>
      <ToastHost>
        {children}
        <UndoToastHost />
      </ToastHost>
    </ActionRunnerProvider>
  );
}
