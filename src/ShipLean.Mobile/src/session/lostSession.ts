import { queryClient } from '../queryClient';
import { session } from './session';

// A request found the session gone (any 401, or 403 password_change_required). Clearing the secret flips the gate to
// Sign in; the server and email stay, so the form comes back filled in.
export function lostSession(): void {
  queryClient.clear();
  session.clearSecret();
}
