import { ImageViewer } from '../features/attachment/viewer/ImageViewer';
import { useImageViewer } from './imageViewer';

/**
 * Renders nothing while the viewer is closed; open, it fills the screen above the page, the top bar, dialogs and the
 * drawer. Its layer is 64, not 90: the ⋯ action sheet (layers 65/70, K5) opens over it and must stay visible.
 */
export function ImageViewerHost() {
  const viewer = useImageViewer();
  return viewer ? <ImageViewer viewer={viewer} /> : null;
}
