import type { SetupStep } from '../links';

// The screen root test ids (K6) and the project tabs (N7). Cross-screen specs (the gate, sign-in success, sign-out,
// the shell) assert only these. Screen-local ids live next to their screen: src/screens/signIn/testIds.ts,
// src/screens/projects/testIds.ts and src/screens/account/testIds.ts, or src/<area>/testIds.ts. Keep this file to the
// roots and the tabs.
export const screenIds = {
  signIn: 'screen-sign-in',
  projects: 'screen-projects',
  account: 'sheet-account',
  // Pinned here because the gate spec reads it: a lost session must come back with the server kept.
  signInServerField: 'sign-in-server-field',
  inbox: 'screen-inbox',
  backlog: 'screen-backlog',
  features: 'screen-features',
  retro: 'screen-retro',
  workItem: 'screen-work-item',
  featureBoard: 'screen-feature-board',
  digest: 'screen-digest',
  settingsVoice: 'screen-settings-voice',
  licences: 'screen-licences',
} as const;

export const screenSetup = (step: SetupStep) => `screen-setup-${step}`;

// The project tab bar (the same ids as T0p2's TabBar) and the key tile on every tab root.
export const tabIds = {
  inbox: 'tab-inbox',
  backlog: 'tab-backlog',
  features: 'tab-features',
  retro: 'tab-retro',
  mic: 'tab-mic',
  inboxBadge: 'tab-inbox-badge',
  rootTile: 'tab-root-tile',
} as const;
