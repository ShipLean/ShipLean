import { useState } from 'react';
import { AddPointDialog } from './addPoint/AddPointDialog';
import { retrosTestIds as ids } from '../testIds';
import type { SprintRetroSlotProps } from './SprintRetroSlotProps';

/** The + Add a point button and, once pressed, its dialog. */
export function AddPoint(props: SprintRetroSlotProps) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        data-testid={ids.retroAddPoint}
        className="cursor-pointer rounded-control border border-edge bg-panel px-3 py-1 text-[length:var(--text-sm)] text-ink hover:bg-canvas"
        onClick={() => setOpen(true)}
      >
        + Add a point
      </button>
      {open ? <AddPointDialog {...props} onClose={() => setOpen(false)} /> : null}
    </>
  );
}
