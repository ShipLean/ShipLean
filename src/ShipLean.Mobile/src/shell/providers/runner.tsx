import { useMemo } from 'react';
import { systemNow } from '../../actions/clock';
import { ActionRunnerProvider } from '../../actions/ActionRunnerProvider';
import { InMemoryActionRunner } from '../../actions/InMemoryActionRunner';
import { useSession } from '../../session/useSession';
import { ToastHost } from '../../ui/Toast';
import type { ProjectSlotProps } from './types';

// One runner per signed-in session: a new one is built when the origin or the email changes.
// No project route renders while signed out, so without an origin and an email the children get no runner.
// TODO: createActionRunner (the persistent outbox) replaces InMemoryActionRunner, which never sends.
export function RunnerProvider({ children }: ProjectSlotProps) {
  const { origin, email } = useSession();
  const signedIn = origin !== null && email !== '';
  const runner = useMemo(
    () => (signedIn ? new InMemoryActionRunner({ now: systemNow }) : null),
    // The runner is keyed by the session, so origin and email are the memo's identity.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [signedIn, origin, email],
  );
  if (runner === null) return children;
  return (
    <ActionRunnerProvider runner={runner}>
      <ToastHost>{children}</ToastHost>
    </ActionRunnerProvider>
  );
}
