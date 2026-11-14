import { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useModalFocus } from '../../../components/useModalFocus';
import { closeImageViewer, type ImageViewerState } from '../../../touch/imageViewer';
import { touchTestIds } from '../../../touch/testIds';
import { attachmentTestIds as ids } from '../testIds';
import { useSwipe } from './useSwipe';
import { useZoom } from './useZoom';
import { ViewerMoreSheet } from './ViewerMoreSheet';
import { ViewerThumbs } from './ViewerThumbs';

const arrow =
  'absolute top-1/2 z-10 grid size-[var(--touch-target)] -translate-y-1/2 place-items-center rounded-full bg-black/50 text-lg text-white';

/**
 * The full-screen image viewer (SHIP-715 D4): always dark whatever the theme. Pinch or double-tap zooms, a drag pans
 * while zoomed and swipes between images when not, and the thumbnails jump to one. ✕ closes; ⋯ offers Download,
 * Copy link and Open original.
 */
export function ImageViewer({ viewer }: { viewer: ImageViewerState }) {
  const { images, context } = viewer;
  const container = useRef<HTMLDivElement>(null);
  useModalFocus(container);
  const [index, setIndex] = useState(() => Math.min(Math.max(viewer.start, 0), images.length - 1));
  const zoom = useZoom();
  const { reset } = zoom;
  const count = images.length;
  const go = (to: number) => {
    if (to < 0 || to >= count) return;
    reset();
    setIndex(to);
  };
  const step = useCallback(
    (by: number) => {
      reset();
      setIndex((current) => Math.min(Math.max(current + by, 0), count - 1));
    },
    [count, reset],
  );
  const swipe = useSwipe(!zoom.zoomed, step);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      // A sheet over the viewer handles its own Esc and marks it.
      if (event.defaultPrevented) return;
      if (event.key === 'Escape') closeImageViewer();
      if (event.key === 'ArrowLeft') step(-1);
      if (event.key === 'ArrowRight') step(1);
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [step]);

  const image = images[index];
  const many = images.length > 1;
  return createPortal(
    <div
      ref={container}
      tabIndex={-1}
      role="dialog"
      aria-modal="true"
      aria-label="Image viewer"
      data-testid={touchTestIds.imageViewer}
      className="fixed inset-0 z-[64] flex flex-col bg-black text-white outline-none"
    >
      <div
        className="flex flex-none items-center gap-1 px-1"
        style={{ paddingTop: 'env(safe-area-inset-top)' }}
      >
        <button
          type="button"
          aria-label="Close"
          data-testid={touchTestIds.imageViewerClose}
          onClick={closeImageViewer}
          className="flex min-h-[var(--touch-target)] min-w-[var(--touch-target)] items-center justify-center text-lg text-white"
        >
          ✕
        </button>
        <span
          data-testid={ids.viewerCounter}
          className="min-w-0 flex-1 truncate text-center text-[length:var(--text-base)]"
        >
          {`${index + 1} of ${images.length}`}
          {context ? ` · ${context}` : ''}
        </span>
        {image ? (
          <ViewerMoreSheet image={image} />
        ) : (
          <span className="size-[var(--touch-target)]" />
        )}
      </div>
      <div className="relative min-h-0 flex-1">
        <div
          data-testid={ids.viewerStage}
          data-zoomed={zoom.zoomed ? 'true' : 'false'}
          className="flex size-full touch-none items-center justify-center overflow-hidden select-none"
          onDragStart={(event) => event.preventDefault()}
          onPointerDown={(event) => {
            zoom.handlers.onPointerDown(event);
            swipe.handlers.onPointerDown(event);
          }}
          onPointerMove={(event) => {
            zoom.handlers.onPointerMove(event);
            swipe.handlers.onPointerMove(event);
          }}
          onPointerUp={(event) => {
            zoom.handlers.onPointerUp(event);
            swipe.handlers.onPointerUp(event);
          }}
          onPointerCancel={(event) => {
            zoom.handlers.onPointerCancel(event);
            swipe.handlers.onPointerCancel(event);
          }}
        >
          {image && (
            <img
              src={image.src}
              alt={image.alt}
              draggable={false}
              data-testid={touchTestIds.imageViewerImage}
              className="max-h-full max-w-full object-contain select-none"
              style={{
                transform: `translate(${zoom.x + swipe.offset}px, ${zoom.y}px) scale(${zoom.scale})`,
                transition: swipe.dragging || zoom.zoomed ? 'none' : 'transform 150ms',
              }}
            />
          )}
        </div>
        {many && index > 0 ? (
          <button
            type="button"
            aria-label="Previous image"
            data-testid={ids.viewerPrev}
            className={`${arrow} left-1`}
            onClick={() => go(index - 1)}
          >
            ‹
          </button>
        ) : null}
        {many && index < images.length - 1 ? (
          <button
            type="button"
            aria-label="Next image"
            data-testid={ids.viewerNext}
            className={`${arrow} right-1`}
            onClick={() => go(index + 1)}
          >
            ›
          </button>
        ) : null}
      </div>
      {many ? <ViewerThumbs images={images} current={index} onSelect={go} /> : null}
    </div>,
    document.body,
  );
}
