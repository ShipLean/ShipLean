import { platform } from '@shiplean/core/platform';
import { clearOutbox } from '../actions/outbox';
import { scopeOf } from '../actions/scope';
import { queryClient } from '../queryClient';
import { session } from './session';

// A request found the session gone (any 401, or 403 password_change_required). Clearing the secret flips the gate to
// Sign in; the server and email stay, so the form comes back filled in.
export function lostSession(): void {
  queryClient.clear();
  const scope = scopeOf(session.get());
  session.clearSecret();
  // The runner's outbox for this session. After clearSecret, so the runner has already stopped (runner.tsx).
  if (scope !== null) clearOutbox(platform().persistentStore(), scope);
}
