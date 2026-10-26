import { useState } from 'react';
import { Dialog } from '../Dialog';
import { harnessTestIds } from './harnessIds';

/** CT-only: an opener button, a control outside, and a dialog with two controls. */
export function DialogHarness() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        data-testid={harnessTestIds.ctDialogOpener}
        onClick={() => setOpen(true)}
      >
        Open
      </button>
      <button type="button" data-testid={harnessTestIds.ctDialogOutside}>
        Outside
      </button>
      {open ? (
        <Dialog
          title="Sprint end"
          onClose={() => setOpen(false)}
          footer={
            <button type="button" data-testid={harnessTestIds.ctDialogLast}>
              Last
            </button>
          }
        >
          <button type="button" data-testid={harnessTestIds.ctDialogFirst}>
            First
          </button>
        </Dialog>
      ) : null}
    </>
  );
}
