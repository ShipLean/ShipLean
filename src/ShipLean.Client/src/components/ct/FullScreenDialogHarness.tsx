import { useState } from 'react';
import { saveBarHarnessIds as ids } from '../../touch/ct/saveBarHarnessIds';
import { Dialog } from '../Dialog';

/**
 * CT-only: an opener and a dialog with a primary action, a footer and a body as long as `rows`. Its width is narrower
 * than a phone, so a desktop inline width leaking onto the touch panel shows up as a box under the screen's width.
 */
export function FullScreenDialogHarness({ rows = 3 }: { rows?: number }) {
  const [open, setOpen] = useState(true);
  return (
    <>
      <button type="button" data-testid={ids.ctDialogOpener} onClick={() => setOpen(true)}>
        Open
      </button>
      <span data-testid={ids.ctDialogState}>{open ? 'open' : 'closed'}</span>
      {open ? (
        <Dialog
          title="New feature"
          width="20rem"
          onClose={() => setOpen(false)}
          primaryAction={
            <button type="button" data-testid={ids.ctDialogPrimaryButton} className="btn-signal">
              Create
            </button>
          }
          footer={<span className="text-[length:var(--text-sm)] text-ink-muted">Draft</span>}
        >
          {Array.from({ length: rows }, (_, row) => (
            <p key={row} className="m-0 border-b border-edge py-2">
              Field {row + 1}
            </p>
          ))}
          <p data-testid={ids.ctDialogBodyEnd} className="m-0">
            Body end
          </p>
        </Dialog>
      ) : null}
    </>
  );
}
