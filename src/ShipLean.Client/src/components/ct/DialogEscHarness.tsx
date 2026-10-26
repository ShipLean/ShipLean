import { useState } from 'react';
import { Dialog } from '../Dialog';
import { harnessTestIds } from './harnessIds';

/** CT-only: a dialog holding a field that handles Esc itself (preventDefault), and a readout of whether it closed. */
export function DialogEscHarness() {
  const [open, setOpen] = useState(true);
  return (
    <>
      <output data-testid={harnessTestIds.ctDialogState}>{open ? 'open' : 'closed'}</output>
      {open ? (
        <Dialog title="Field" onClose={() => setOpen(false)}>
          <input
            data-testid={harnessTestIds.ctDialogEscField}
            onKeyDown={(event) => {
              if (event.key === 'Escape') event.preventDefault();
            }}
          />
        </Dialog>
      ) : null}
    </>
  );
}
