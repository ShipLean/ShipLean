import { createContext } from 'react';

/** True under a TouchPager: a touch drag there turns the page at the screen edges, so its ghost shows the edge hints. */
export const InsidePagerContext = createContext(false);
