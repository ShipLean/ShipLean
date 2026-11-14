import { createContext } from 'react';

// At 900px and below the toolbar opens the search field under its row, chosen from ⋯ More. It provides this so the field
// shows a Cancel that clears the search and closes the row. Absent (null) above 900px, where the field is always there.
export const TouchSearchClose = createContext<(() => void) | null>(null);
