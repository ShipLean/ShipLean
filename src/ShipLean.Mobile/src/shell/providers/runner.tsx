import { useMemo } from 'react';
import { systemNow } from '../../actions/clock';
import { ActionRunnerProvider } from '../../actions/ActionRunnerProvider';
import { InMemoryActionRunner } from '../../actions/InMemoryActionRunner';
import { UndoToastHost } from '../../actions/UndoToastHost';
import { useSession } from '../../session/useSession';
import { ToastHost } from '../../ui/Toast';
import type { ProjectSlotProps } from './types';

// One runner per signed-in session: a new one is built when the origin or the email changes.
// No project route renders while signed out, so without an origin and an email the children get no runner.
// TODO: createActionRunner (the persistent outbox) replaces InMemoryActionRunner, which never sends.
export function RunnerProvider({ children }: ProjectSlotProps) {
  const { origin, email } = useSession();
  // The session scope (origin + email) is the runner's identity.
  const scope = origin !== null && email !== '' ? `${origin}\n${email}` : null;
  const runner = useMemo(
    () => (scope === null ? null : new InMemoryActionRunner({ now: systemNow })),
    [scope],
  );
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
