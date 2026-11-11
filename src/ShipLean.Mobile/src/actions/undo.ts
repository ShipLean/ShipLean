import type { UndoWindow } from './types';

// The only undo windows. The Inbox holds a tap for 5 s; typed and voice changes are sent at once and can be undone
// for 8 s on the toast; a write with no undo uses none. There is no other hold override.
export const UNDO = {
  tap: { mode: 'hold', ms: 5000 },
  typed: { mode: 'after', ms: 8000 },
  voice: { mode: 'after', ms: 8000 },
  none: { mode: 'hold', ms: 0 },
} as const satisfies Record<string, UndoWindow>;

// How long the voice change row in Notes offers Undo, and how long sent entries stay in pending().
export const VOICE_ROW_MS = 600_000;
