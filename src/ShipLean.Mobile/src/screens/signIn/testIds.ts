// Screen-local test ids for Sign in. The roots and the server field are in ../testIds.ts (K6); the field's hint and
// error lines derive theirs from it (ui/testIds.ts): the check line and the address errors.
export const signInIds = {
  button: 'sign-in-button',
  error: 'sign-in-error',
  version: 'sign-in-version',
  body: 'sign-in-body',
  connectedDot: 'sign-in-connected-dot',
  howCard: 'sign-in-how-card',
} as const;
