import { useContext, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { InsidePagerContext } from './insidePager';
import { touchTestIds } from './testIds';

// The lifted copy that follows the finger during a touch drag (K6, W01-P2's addition). It sits at z 80 on the shared
// ladder: above the top bar (30), SaveBar (40), dialogs (50), drawer (60/61) and sheets (65/70); below the image viewer
// (90). pointer-events: none, or document.elementFromPoint would find the ghost instead of the drop target under it.
// Rendered into <body>, so no transformed ancestor can capture its fixed position. The lane renders the original
// faded where it was. Under a TouchPager, holding at a screen edge turns the page, so ‹ and › hints sit at both edges
// while the card is lifted (design fboard-drag, tboard-drag, board-drag).
export function TouchDragGhost({ x, y, children }: { x: number; y: number; children: ReactNode }) {
  const inPager = useContext(InsidePagerContext);
  return createPortal(
    <>
      {inPager ? (
        <>
          <EdgeHint edge="left" />
          <EdgeHint edge="right" />
        </>
      ) : null}
      <div
        data-testid={touchTestIds.dragGhost}
        aria-hidden="true"
        className="pointer-events-none fixed top-0 left-0 z-80 w-max max-w-[calc(100vw-32px)] rounded-md bg-panel text-ink shadow-[0_18px_36px_rgb(0_0_0/0.3),0_0_0_2px_var(--color-accent)]"
        style={{ transform: `translate(${x}px, ${y}px) translate(-50%, -50%) scale(1.03)` }}
      >
        {children}
      </div>
    </>,
    document.body,
  );
}

function EdgeHint({ edge }: { edge: 'left' | 'right' }) {
  return (
    <span
      data-testid={touchTestIds.dragEdgeHint}
      data-edge={edge}
      aria-hidden="true"
      className={`pointer-events-none fixed top-1/2 z-80 flex h-12 w-6 -translate-y-1/2 items-center justify-center text-[length:var(--text-lg)] leading-none text-accent/70 ${edge === 'left' ? 'left-0' : 'right-0'}`}
    >
      {edge === 'left' ? '‹' : '›'}
    </span>
  );
}
