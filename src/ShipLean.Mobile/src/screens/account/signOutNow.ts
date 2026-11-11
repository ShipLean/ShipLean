import { endCurrentDeviceSession } from '@shiplean/core/api/deviceSessions';
import { platform } from '@shiplean/core/platform';
import { scopeOf } from '../../actions/scope';
import { clearAllDrafts } from '../../dock/drafts';
import { session } from '../../session/session';
import { lostSession } from '../../session/lostSession';
import { signOut } from '../../session/signOut';

// The app's Sign out, wired to the real session and server.
export const signOutNow = () => {
  // Drafts go on an explicit sign-out only; a lost session keeps them. The scope is read before the session clears.
  const scope = scopeOf(session.get());
  if (scope !== null) clearAllDrafts(platform().persistentStore(), scope);
  return signOut({ endSession: endCurrentDeviceSession, clear: lostSession });
};
