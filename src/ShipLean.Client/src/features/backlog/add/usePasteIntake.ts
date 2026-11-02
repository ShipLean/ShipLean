import type { ClipboardEventHandler } from 'react';

/** Props for the new-item row's title input. */
export interface PasteIntake {
  onPaste?: ClipboardEventHandler<HTMLInputElement>;
}

const none: PasteIntake = {};

/** Sends several pasted lines to the paste review. */
// TODO: reading a multi-line paste replaces this empty set.
export function usePasteIntake(): PasteIntake {
  return none;
}
