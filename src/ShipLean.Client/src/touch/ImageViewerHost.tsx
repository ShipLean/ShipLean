import { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { useModalFocus } from '../components/useModalFocus';
import { closeImageViewer, useImageViewer, type ImageViewerState } from './imageViewer';
import { touchTestIds } from './testIds';

// TODO: real viewer (zoom, swipe through images, ⋯ sheet)
function Viewer({ viewer }: { viewer: ImageViewerState }) {
  const container = useRef<HTMLDivElement>(null);
  useModalFocus(container);
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') closeImageViewer();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, []);
  const image = viewer.images[viewer.start];
  return createPortal(
    <div
      ref={container}
      tabIndex={-1}
      role="dialog"
      aria-modal="true"
      aria-label="Image viewer"
      data-testid={touchTestIds.imageViewer}
      className="fixed inset-0 z-[90] flex items-center justify-center bg-black outline-none"
    >
      {image && (
        <img
          src={image.src}
          alt={image.alt}
          data-testid={touchTestIds.imageViewerImage}
          className="max-h-full max-w-full object-contain"
        />
      )}
      <button
        type="button"
        aria-label="Close"
        data-testid={touchTestIds.imageViewerClose}
        onClick={closeImageViewer}
        className="absolute left-0 flex min-h-[var(--touch-target)] min-w-[var(--touch-target)] items-center justify-center text-lg text-white"
        style={{ top: 'env(safe-area-inset-top)' }}
      >
        ✕
      </button>
    </div>,
    document.body,
  );
}

/** Renders nothing while the viewer is closed; open, it fills the screen above everything else (layer 90). */
export function ImageViewerHost() {
  const viewer = useImageViewer();
  return viewer ? <Viewer viewer={viewer} /> : null;
}
