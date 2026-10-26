import { api, clientId } from '../api/client';
import { createProjectConnection } from './connection';
import { createRealtime } from './createRealtime';

// The tab's one real-time client. The connection is made when the first project is wanted, not on import.
export const realtime = createRealtime({
  connect: () => createProjectConnection(),
  ownClientId: clientId,
  // Any API call answers a lost session the way the whole app does: 401 session_ended, password_change_required or a
  // plain 401 each send the browser to the sign-in page in the right state (api/client.ts).
  confirmSession: () =>
    api.get('/api/me').then(
      () => true,
      () => false,
    ),
});
