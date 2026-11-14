import { createContext, useContext } from 'react';

/** The `data-drop-target` of the card or cell a lifted card is over (it shows the drop line), or null. */
export interface TouchOver {
  over: string | null;
  setOver: (target: string | null) => void;
}

export const TouchOverContext = createContext<TouchOver>({ over: null, setOver: () => {} });

export const useTouchOver = (): TouchOver => useContext(TouchOverContext);
