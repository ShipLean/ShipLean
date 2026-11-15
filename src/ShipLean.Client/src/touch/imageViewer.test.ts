import { afterEach, expect, it } from 'vitest';
import { closeImageViewer, getImageViewerState, openImageViewer } from './imageViewer';

afterEach(() => closeImageViewer());

const images = [
  { src: '/a.png', alt: 'A', name: 'a.png' },
  { src: '/b.png', alt: 'B', name: 'b.png' },
];

it('openImageViewer_WhenCalled_ShouldPublishTheImagesAndStart', () => {
  openImageViewer({ images, start: 1, context: 'SHIP-1' });
  expect(getImageViewerState()).toEqual({ images, start: 1, context: 'SHIP-1' });
});

it('closeImageViewer_WhenCalled_ShouldClear', () => {
  openImageViewer({ images, start: 0 });
  closeImageViewer();
  expect(getImageViewerState()).toBeNull();
});
