import type { SessionState } from '../session/sessionStore';

// The one scope format: the runner and its outbox belong to one server and one person. It comes from SessionState,
// which is synchronous and works offline. null before there is both a server and an email.
export function scopeOf({ origin, email }: Pick<SessionState, 'origin' | 'email'>): string | null {
  return origin !== null && email !== '' ? `${origin}\n${email}` : null;
}
