import { endCurrentDeviceSession } from '@shiplean/core/api/deviceSessions';
import { lostSession } from '../../session/lostSession';
import { signOut } from '../../session/signOut';

// The app's Sign out, wired to the real session and server.
export const signOutNow = () =>
  signOut({ endSession: endCurrentDeviceSession, clear: lostSession });
