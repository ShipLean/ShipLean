import { createContext } from 'react';

// At 900px and below the toolbar is one row and the filter's chips sit under it. The toolbar provides the element they go
// into (null until it mounts); FilterButton portals its chips there. Absent (undefined) above 900px, where the chips stay
// in the row.
export const ToolbarChipsHost = createContext<{ host: HTMLElement | null } | undefined>(undefined);
