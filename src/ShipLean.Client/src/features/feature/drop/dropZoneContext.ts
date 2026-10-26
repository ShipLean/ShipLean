import { createContext, useContext } from 'react';

/** Opens the drop zone's file chooser, so "+ Attach" is checked exactly like a drop. */
export const DropZoneContext = createContext<(() => void) | null>(null);

export function useChooseFiles(): () => void {
  const choose = useContext(DropZoneContext);
  if (choose === null)
    throw new Error('useChooseFiles must be called inside the Feature drop zone.');
  return choose;
}
