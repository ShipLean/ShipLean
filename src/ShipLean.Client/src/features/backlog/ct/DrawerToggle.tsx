import { useState } from 'react';
import { QuickSearchProvider } from '../../quickSearch/host/QuickSearchProvider';
import { TouchDrawer } from '../../../touch/TouchDrawer';

// CT-only: a button that opens the drawer, which comes first in the page so only the bar's layer can put it under the scrim.
export function DrawerToggle() {
  const [open, setOpen] = useState(false);
  return (
    <>
      {open ? (
        <QuickSearchProvider>
          <TouchDrawer onClose={() => setOpen(false)} />
        </QuickSearchProvider>
      ) : null}
      <button type="button" data-testid="ct-open-drawer" onClick={() => setOpen(true)}>
        Drawer
      </button>
    </>
  );
}
