import { useEffect } from 'react';
import { ImageViewerHost, openImageViewer } from '../../../../touch/imageViewer';

const picture = (label: string, color: string) =>
  `data:image/svg+xml;utf8,${encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="900" height="600"><rect width="100%" height="100%" fill="${color}"/><text x="40" y="100" font-size="72" fill="white">${label}</text></svg>`,
  )}`;

const images = [
  { src: picture('Checkout flow', '#3b6ea8'), alt: 'Checkout flow', name: 'checkout-flow.svg' },
  { src: picture('Card picker', '#a83b3b'), alt: 'Card picker', name: 'card-picker.svg' },
  { src: picture('Receipt', '#3ba85a'), alt: 'Receipt', name: 'receipt.svg' },
];

/** The image viewer open on a seeded set of three pictures (CT and screenshots). */
export function ImageViewerHarness({ start = 0 }: { start?: number }) {
  useEffect(() => openImageViewer({ images, start, context: 'SHIP-101' }), [start]);
  return <ImageViewerHost />;
}
