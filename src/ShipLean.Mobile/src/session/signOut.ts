// Sign out: forget the session here at once and ask the server to end it, whatever it answers. A phone that is
// offline, or a server that says 404, must still end up signed out: the secret never outlives the tap.
export interface SignOutDeps {
  // DELETE /api/auth/device-sessions/current with the bearer.
  endSession: () => Promise<unknown>;
  // lostSession: clears the query cache and the secret; the server and email stay for the prefill.
  clear: () => void;
}

export function signOut({ endSession, clear }: SignOutDeps): Promise<void> {
  // The core reads the bearer and origin before its first await, so the DELETE still carries them after clear().
  const ending = endSession();
  clear();
  // Nobody waits on the network: a black-holed request must not leave the person signed in. Whatever the server
  // answers (offline, 404, 5xx), the phone is already signed out; a session the server keeps expires there.
  return ending.then(
    () => undefined,
    () => undefined,
  );
}
