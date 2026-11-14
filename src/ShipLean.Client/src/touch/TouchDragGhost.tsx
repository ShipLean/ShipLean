import type { ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { touchTestIds } from './testIds';

// The lifted copy that follows the finger during a touch drag (K6, W01-P2's addition). It sits at z 80 on the shared
// ladder: above the top bar (30), SaveBar (40), dialogs (50), drawer (60/61) and sheets (65/70); below the image viewer
// (90). pointer-events: none, or document.elementFromPoint would find the ghost instead of the drop target under it.
// Rendered into <body>, so no transformed ancestor can capture its fixed position. The lane renders the original
// faded where it was.
export function TouchDragGhost({ x, y, children }: { x: number; y: number; children: ReactNode }) {
  return createPortal(
    <div
      data-testid={touchTestIds.dragGhost}
      aria-hidden="true"
      className="pointer-events-none fixed top-0 left-0 z-80 w-max max-w-[calc(100vw-32px)] rounded-md bg-panel text-ink shadow-[0_18px_36px_rgb(0_0_0/0.3),0_0_0_2px_var(--color-accent)]"
      style={{ transform: `translate(${x}px, ${y}px) translate(-50%, -50%) scale(1.03)` }}
    >
      {children}
    </div>,
    document.body,
  );
}
