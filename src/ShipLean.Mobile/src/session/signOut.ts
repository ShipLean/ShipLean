// Sign out: end this phone's session on the server, then forget it here whatever the server answered. A phone that is
// offline, or a server that says 404, must still end up signed out: the secret never outlives the tap.
export interface SignOutDeps {
  // DELETE /api/auth/device-sessions/current with the bearer.
  endSession: () => Promise<unknown>;
  // lostSession: clears the query cache and the secret; the server and email stay for the prefill.
  clear: () => void;
}

export async function signOut({ endSession, clear }: SignOutDeps): Promise<void> {
  try {
    await endSession();
  } catch {
    // Whatever went wrong (offline, 404, 5xx) the phone still signs out; a session the server keeps expires there.
  } finally {
    clear();
  }
}
