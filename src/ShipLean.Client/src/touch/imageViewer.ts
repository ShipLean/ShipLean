// K7: the image viewer's API and store. `openImageViewer` is called by Markdown when an attached image is tapped at
// touch; `ImageViewerHost` (mounted once, in AppRoutes) shows it.
import { useSyncExternalStore } from 'react';

export interface ViewerImage {
  src: string;
  alt: string;
  name: string;
}

export interface ImageViewerState {
  images: readonly ViewerImage[];
  /** The index in `images` the viewer opens on. */
  start: number;
  /** What the images belong to, such as the work item's key. */
  context?: string;
}

let state: ImageViewerState | null = null;
const listeners = new Set<() => void>();

function publish(next: ImageViewerState | null) {
  state = next;
  for (const listener of listeners) listener();
}

export function openImageViewer(open: ImageViewerState) {
  publish({ images: open.images, start: open.start, context: open.context });
}

export function closeImageViewer() {
  publish(null);
}

/** The viewer's current state, or null while it is closed. */
export function getImageViewerState(): ImageViewerState | null {
  return state;
}

export function useImageViewer(): ImageViewerState | null {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => state,
  );
}

export { ImageViewerHost } from './ImageViewerHost';
