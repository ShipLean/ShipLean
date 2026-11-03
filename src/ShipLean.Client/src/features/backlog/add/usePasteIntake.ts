import { createContext, useContext, type ClipboardEventHandler } from 'react';
import type { WorkItemTypeId } from '../../../api/refs';
import { lineCount } from './pasteParse';

/** Opens the paste review on some text, with the type that rows without a `bug:` prefix get. */
export type OpenPaste = (text: string, defaultType: WorkItemTypeId) => void;

/** Provided by PasteHost; absent outside it, where pasting stays a normal paste. */
export const PasteContext = createContext<OpenPaste | null>(null);

/** Props for the new-item row's title input. */
export interface PasteIntake {
  onPaste?: ClipboardEventHandler<HTMLInputElement>;
}

const none: PasteIntake = {};

/** Sends two or more pasted lines to the paste review; one line stays a normal paste. */
export function usePasteIntake(defaultType: WorkItemTypeId = 'story'): PasteIntake {
  const open = useContext(PasteContext);
  if (open === null) return none;
  return {
    onPaste: (event) => {
      const text = event.clipboardData.getData('text');
      if (lineCount(text) < 2) return;
      event.preventDefault();
      open(text, defaultType);
    },
  };
}
