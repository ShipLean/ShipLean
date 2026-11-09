// The screen root test ids (K6). Cross-screen specs (the gate, sign-in success, sign-out) assert only these.
// Screen-local ids live next to their screen: src/screens/signIn/testIds.ts, src/screens/projects/testIds.ts and
// src/screens/account/testIds.ts. Keep this file to the roots.
export const screenIds = {
  signIn: 'screen-sign-in',
  projects: 'screen-projects',
  account: 'sheet-account',
  // Pinned here because the gate spec reads it: a lost session must come back with the server kept.
  signInServerField: 'sign-in-server-field',
} as const;
